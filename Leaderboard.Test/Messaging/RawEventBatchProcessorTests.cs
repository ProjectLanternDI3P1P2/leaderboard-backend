using System.Threading.Channels;
using Combat.Contracts.Events.V1;
using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Messaging;
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

    private static RawEventDelivery Valid(ulong tag, string? causationId = null) =>
        new(
            tag,
            new MessageEnvelopeMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                CorrelationId = "correlation",
                CausationId = causationId ?? string.Empty,
                Type = "combat.finished",
                Version = 1,
                OccurredAtUtc = "2026-10-19T11:59:00.0000000+00:00",
                Producer = "combat",
                Payload = ByteString.CopyFrom([1, 2, 3]),
            }.ToByteArray(),
            "tx-1",
            ReceivedAt
        );

    [Fact]
    public async Task ProcessAsync_StoresValidMessagesThenAcksThem()
    {
        IReadOnlyCollection<RawEvent>? stored = null;
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IReadOnlyCollection<RawEvent>, CancellationToken>(
                (events, _) => stored = events
            )
            .Returns(Task.CompletedTask);

        await CreateProcessor()
            .ProcessAsync(
                [Valid(1, "cause"), Valid(2)],
                Acknowledger(),
                TestContext.Current.CancellationToken
            );

        acked.Should().Equal(1UL, 2UL);
        rejected.Should().BeEmpty();
        stored.Should().NotBeNull();
        stored!.Should().HaveCount(2);
        stored
            .First()
            .Should()
            .BeEquivalentTo(
                new
                {
                    TransactionId = "tx-1",
                    CausationId = "cause",
                    ReceivedAt,
                    Producer = "combat",
                    Payload = new byte[] { 1, 2, 3 },
                },
                options => options.ExcludingMissingMembers()
            );
        stored.Last().CausationId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAsync_RejectsUnreadableMessagesAndKeepsTheRest()
    {
        var unreadable = new RawEventDelivery(1, [0xFF, 0xFF, 0xFF], null, ReceivedAt);
        var badId = new RawEventDelivery(
            2,
            new MessageEnvelopeMessage
            {
                MessageId = "not-a-guid",
                OccurredAtUtc = "x",
            }.ToByteArray(),
            null,
            ReceivedAt
        );

        await CreateProcessor()
            .ProcessAsync(
                [unreadable, badId, Valid(3)],
                Acknowledger(),
                TestContext.Current.CancellationToken
            );

        rejected.Should().Equal(1UL, 2UL);
        acked.Should().Equal(3UL);
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
    public async Task ProcessAsync_DoesNotWriteWhenNothingIsReadable()
    {
        await CreateProcessor()
            .ProcessAsync(
                [new RawEventDelivery(1, [0xFF], null, ReceivedAt)],
                Acknowledger(),
                TestContext.Current.CancellationToken
            );

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
