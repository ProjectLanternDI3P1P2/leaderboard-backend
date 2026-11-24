using System.Threading.Channels;
using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Messaging;
using Leaderboard.Test.Collection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leaderboard.Test.Messaging;

public sealed class RawEventBatchProcessorTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRawEventStore> store = new();
    private readonly List<ulong> acked = [];
    private readonly List<ulong> rejected = [];

    private static RawEventConsumerOptions Settings(int batchSize = 10) =>
        new()
        {
            BatchSize = batchSize,
            BatchTimeout = TimeSpan.FromMilliseconds(50),
            MaxAttempts = 2,
            InitialBackoff = TimeSpan.Zero,
        };

    private RawEventBatchProcessor CreateProcessor(RawEventConsumerOptions? settings = null)
    {
        IOptions<RawEventConsumerOptions> options = Options.Create(settings ?? Settings());
        return new RawEventBatchProcessor(
            new RawEventBatchWriter(
                store.Object,
                options,
                NullLogger<RawEventBatchWriter>.Instance
            ),
            options,
            NullLogger<RawEventBatchProcessor>.Instance
        );
    }

    private IDeliveryAcknowledger Acknowledger()
    {
        var acknowledger = new Mock<IDeliveryAcknowledger>();
        acknowledger
            .Setup(a => a.AckAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, CancellationToken>((tag, _) => acked.Add(tag))
            .Returns(Task.CompletedTask);
        acknowledger
            .Setup(a => a.RejectAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .Callback<ulong, CancellationToken>((tag, _) => rejected.Add(tag))
            .Returns(Task.CompletedTask);
        return acknowledger.Object;
    }

    private static RawEventDelivery Valid(ulong tag)
    {
        var envelope = EventFixtures.Load("hero-created.json");
        envelope.MessageId = Guid.NewGuid().ToString();
        return new(
            tag,
            envelope.ToByteArray(),
            ReceivedAt,
            "player.events",
            "player.hero-created.v1",
            envelope.MessageId
        );
    }

    [Fact]
    public async Task ProcessAsync_PreservesWholeBodyAndMetadataBeforeAcknowledging()
    {
        var delivery = Valid(1);
        var envelope = GameEventEnvelope.Parser.ParseFrom(delivery.Body);
        // Unknown wire fields and repeated scalar encoding must survive archival.
        delivery = delivery with
        {
            Body = [.. delivery.Body, 8, 1, 160, 6, 42],
        };
        byte[] expected = delivery.Body.ToArray();
        var entered = new TaskCompletionSource<RawEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var confirmed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (IReadOnlyCollection<RawEvent> events, CancellationToken _) =>
                {
                    entered.SetResult(events.Single());
                    await confirmed.Task;
                }
            );
        var pending = CreateProcessor()
            .ProcessAsync([delivery], Acknowledger(), TestContext.Current.CancellationToken);
        var stored = await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );
        acked.Should().BeEmpty();
        Array.Fill<byte>(delivery.Body, 0);
        stored.Payload.Should().Equal(expected);
        stored.MessageId.Should().Be(Guid.Parse(envelope.MessageId));
        stored.TransactionId.Should().Be(envelope.TransactionId);
        stored.Producer.Should().Be("Player");
        stored.ReceivedAt.Should().Be(ReceivedAt);
        stored.OccurredAtUtc.Should().Be(envelope.OccurredAt.ToDateTimeOffset());
        stored.ParseStatus.Should().Be("Valid");
        stored.Id.Should().NotBe(stored.MessageId!.Value);
        confirmed.SetResult();
        await pending;
        acked.Should().Equal(1UL);
        rejected.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_ArchivesUnreadableInvalidAndUnknownBodies()
    {
        List<RawEvent> stored = [];
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyCollection<RawEvent>, CancellationToken>(
                (events, _) => stored.AddRange(events)
            )
            .Returns(Task.CompletedTask);
        var invalid = EventFixtures.Load("hero-created.json");
        invalid.MessageId = "not-a-guid";
        invalid.OccurredAt.Seconds = long.MaxValue;
        var unknown = EventFixtures.Load("hero-created.json");
        unknown.EnvelopeVersion = 99;
        RawEventDelivery[] deliveries =
        [
            new(1, [255], ReceivedAt),
            new(2, invalid.ToByteArray(), ReceivedAt),
            new(3, unknown.ToByteArray(), ReceivedAt),
            Valid(4),
        ];
        await CreateProcessor()
            .ProcessAsync(deliveries, Acknowledger(), TestContext.Current.CancellationToken);
        stored
            .Select(e => e.ParseStatus)
            .Should()
            .Equal("Unreadable", "InvalidOrUnknown", "InvalidOrUnknown", "Valid");
        for (int i = 0; i < deliveries.Length; i++)
            stored[i].Payload.Should().Equal(deliveries[i].Body);
        stored[0].MessageId.Should().BeNull();
        stored[1].OccurredAtUtc.Should().BeNull();
        stored.Take(3).Should().OnlyContain(e => e.ValidationErrors.Length > 0);
        acked.Should().Equal(1UL, 2UL, 3UL, 4UL);
        rejected.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_RoutingMismatchIsArchivedWithValidationErrors()
    {
        RawEvent? stored = null;
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyCollection<RawEvent>, CancellationToken>(
                (events, _) => stored = events.Single()
            )
            .Returns(Task.CompletedTask);
        await CreateProcessor()
            .ProcessAsync(
                [Valid(1) with { RoutingKey = "combat.skill-used.v1" }],
                Acknowledger(),
                TestContext.Current.CancellationToken
            );
        stored!.ParseStatus.Should().Be("InvalidOrUnknown");
        stored.ValidationErrors.Should().Contain("AMQP routing key does not match the envelope.");
        acked.Should().Equal(1UL);
    }

    [Fact]
    public async Task ProcessAsync_RejectsTheBatchWhenTheStoreKeepsFailing()
    {
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException());

        await CreateProcessor()
            .ProcessAsync(
                [Valid(1), Valid(2)],
                Acknowledger(),
                TestContext.Current.CancellationToken
            );

        acked.Should().BeEmpty();
        rejected.Should().Equal(1UL, 2UL);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotWriteAnEmptyBatch()
    {
        await CreateProcessor()
            .ProcessAsync([], Acknowledger(), TestContext.Current.CancellationToken);

        store.Verify(
            s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task ReadBatchAsync_StopsAtTheBatchSize()
    {
        Channel<RawEventDelivery> channel = Channel.CreateUnbounded<RawEventDelivery>();
        foreach (ulong tag in new ulong[] { 1, 2, 3 })
        {
            await channel.Writer.WriteAsync(Valid(tag), TestContext.Current.CancellationToken);
        }

        List<RawEventDelivery> batch = await CreateProcessor(Settings(batchSize: 2))
            .ReadBatchAsync(channel.Reader, TestContext.Current.CancellationToken);

        batch.Select(d => d.Tag).Should().Equal(1UL, 2UL);
    }

    [Fact]
    public async Task ReadBatchAsync_ReturnsWhatItHasWhenTheWindowElapses()
    {
        Channel<RawEventDelivery> channel = Channel.CreateUnbounded<RawEventDelivery>();
        await channel.Writer.WriteAsync(Valid(1), TestContext.Current.CancellationToken);

        List<RawEventDelivery> batch = await CreateProcessor()
            .ReadBatchAsync(channel.Reader, TestContext.Current.CancellationToken);

        batch.Select(d => d.Tag).Should().Equal(1UL);
    }
}
