using FluentAssertions;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Leaderboard.Test.Messaging;

public sealed class RawEventBatchWriterTests
{
    private static readonly RawEvent[] Batch =
    [
        new()
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            Type = "combat.finished",
            Version = 1,
            CorrelationId = "c",
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ReceivedAt = DateTimeOffset.UtcNow,
            Producer = "combat",
            Payload = [1],
        },
    ];

    private static RawEventBatchWriter CreateWriter(Mock<IRawEventStore> store) =>
        new(
            store.Object,
            Options.Create(
                new RawEventConsumerOptions { MaxAttempts = 3, InitialBackoff = TimeSpan.Zero }
            ),
            NullLogger<RawEventBatchWriter>.Instance
        );

    [Fact]
    public async Task TryWriteAsync_RetriesUntilTheStoreSucceeds()
    {
        var store = new Mock<IRawEventStore>();
        store
            .SetupSequence(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException())
            .Returns(Task.CompletedTask);

        bool stored = await CreateWriter(store)
            .TryWriteAsync(Batch, TestContext.Current.CancellationToken);

        stored.Should().BeTrue();
        store.Verify(
            s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task TryWriteAsync_ReturnsFalseOnceAttemptsAreExhausted()
    {
        var store = new Mock<IRawEventStore>();
        store
            .Setup(s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new IOException());

        bool stored = await CreateWriter(store)
            .TryWriteAsync(Batch, TestContext.Current.CancellationToken);

        stored.Should().BeFalse();
        store.Verify(
            s =>
                s.AppendAsync(
                    It.IsAny<IReadOnlyCollection<RawEvent>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Exactly(3)
        );
    }
}
