using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Application.Collection;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Collection;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Moq;

namespace Leaderboard.Test.Collection;

public sealed class ArchiveEventTests
{
    [Fact]
    public async Task CopiesOriginalBodyAndAcknowledgesOnlyAfterConfirmedWrite()
    {
        var repository = new Mock<IRawEventRepository>();
        var clock = new Mock<IClock>();
        var now = DateTimeOffset.Parse("2026-10-20T12:00:00Z");
        clock.SetupGet(x => x.UtcNow).Returns(now);
        var entered = new TaskCompletionSource<RawEventReceipt>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository
            .Setup(x => x.AddAsync(It.IsAny<RawEventReceipt>(), It.IsAny<CancellationToken>()))
            .Returns(
                async (RawEventReceipt receipt, CancellationToken _) =>
                {
                    entered.SetResult(receipt);
                    await written.Task;
                }
            );
        var message = Fixture();
        // Legal unknown wire field plus duplicate scalar encoding: reserialization would alter bytes.
        var body = message.ToByteArray().Concat(new byte[] { 8, 1, 160, 6, 42 }).ToArray();
        var expected = body.ToArray();
        var acknowledged = false;
        var pending = new ArchiveEvent(repository.Object, clock.Object).HandleAsync(
            body,
            Metadata(),
            _ =>
            {
                acknowledged = true;
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken
        );
        var receipt = await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken
        );

        acknowledged.Should().BeFalse();
        Array.Fill<byte>(body, 0); // Simulate RabbitMQ returning/reusing its callback buffer.
        receipt.Body.Should().Equal(expected);
        receipt.ReceivedAt.Should().Be(now);
        receipt.TransactionId.Should().Be(message.TransactionId);
        receipt.ProducerSquad.Should().Be("Player");
        receipt.ParseStatus.Should().Be("Valid");
        receipt.BrokerMessageId.Should().Be("transport-id");
        receipt.ReceiptId.Should().NotBe(message.MessageId).And.NotBe(message.TransactionId);
        written.SetResult();
        await pending;
        acknowledged.Should().BeTrue();
        clock.VerifyGet(x => x.UtcNow, Times.Once);
    }

    [Fact]
    public async Task FailedOrUncertainWriteNeverAcknowledges()
    {
        var repository = new Mock<IRawEventRepository>();
        repository
            .Setup(x => x.AddAsync(It.IsAny<RawEventReceipt>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("write confirmation lost"));
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        var ack = new Mock<Func<CancellationToken, Task>>();

        await Assert.ThrowsAsync<IOException>(() =>
            new ArchiveEvent(repository.Object, clock.Object).HandleAsync(
                Fixture().ToByteArray(),
                Metadata(),
                ack.Object,
                TestContext.Current.CancellationToken
            )
        );

        ack.Verify(x => x(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnreadableAndUnknownMessagesAreArchived(bool unreadable)
    {
        var repository = new Mock<IRawEventRepository>();
        RawEventReceipt? actual = null;
        repository
            .Setup(x => x.AddAsync(It.IsAny<RawEventReceipt>(), It.IsAny<CancellationToken>()))
            .Callback<RawEventReceipt, CancellationToken>((r, _) => actual = r)
            .Returns(Task.CompletedTask);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.UtcNow);
        var message = Fixture();
        message.EnvelopeVersion = 99;
        byte[] bytes = unreadable ? [255] : message.ToByteArray();

        await new ArchiveEvent(repository.Object, clock.Object).HandleAsync(
            bytes,
            Metadata(),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        actual!.Body.Should().Equal(bytes);
        actual.ParseStatus.Should().Be(unreadable ? "Unreadable" : "InvalidOrUnknown");
        actual.ValidationErrors.Should().NotBeEmpty();
    }

    internal static GameEventEnvelope Fixture() =>
        JsonParser.Default.Parse<GameEventEnvelope>(
            File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "CollectionFixtures", "hero-created.json")
            )
        );

    private static DeliveryMetadata Metadata() =>
        new("player.events", "player.hero-created.v1", "transport-id", "run-id", false);
}
