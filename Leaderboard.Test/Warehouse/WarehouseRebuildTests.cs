using FluentAssertions;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Exceptions;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Test.Warehouse;

public class WarehouseRebuildTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Request_Now_IsPendingAndBoundedByTheRequestTime()
    {
        // Act
        WarehouseRebuild rebuild = WarehouseRebuild.Request(Now);

        // Assert
        rebuild.Status.Should().Be(WarehouseRebuildStatus.Pending);
        rebuild.ConsolidatedUntil.Should().Be(Now);
        rebuild.GenerationId.Should().NotBeEmpty();
        rebuild.IsActive.Should().BeTrue();
    }

    [Fact]
    public void RecordBatch_TwoBatches_AccumulatesCountersAndMovesTheCursor()
    {
        // Arrange
        WarehouseRebuild rebuild = WarehouseRebuild.Request(Now);
        rebuild.Start(Now);
        Guid lastId = Guid.NewGuid();

        // Act
        rebuild.RecordBatch(
            new WarehouseBatchProgress(10, 8, 1, 1, Now.AddDays(-2), [Guid.NewGuid()]),
            Now
        );
        rebuild.RecordBatch(new WarehouseBatchProgress(5, 5, 0, 0, Now.AddDays(-1), [lastId]), Now);

        // Assert
        rebuild.EventsProcessed.Should().Be(15);
        rebuild.EventsApplied.Should().Be(13);
        rebuild.DuplicatesIgnored.Should().Be(1);
        rebuild.EventsIgnored.Should().Be(1);
        rebuild.LatestEventReceivedAt.Should().Be(Now.AddDays(-1));
        rebuild.CursorEventIds.Should().Equal(lastId);
    }

    [Fact]
    public void Resume_Interrupted_BecomesPendingAndKeepsItsProgress()
    {
        // Arrange
        WarehouseRebuild rebuild = WarehouseRebuild.Request(Now);
        rebuild.Start(Now);
        rebuild.RecordBatch(new WarehouseBatchProgress(10, 10, 0, 0, Now.AddDays(-1), []), Now);
        rebuild.Interrupt(Now);

        // Act
        rebuild.Resume(Now.AddMinutes(5));

        // Assert
        rebuild.Status.Should().Be(WarehouseRebuildStatus.Pending);
        rebuild.EventsProcessed.Should().Be(10);
    }

    [Theory]
    [InlineData(WarehouseRebuildStatus.Pending)]
    [InlineData(WarehouseRebuildStatus.Running)]
    [InlineData(WarehouseRebuildStatus.Completed)]
    public void Resume_NotInterruptedNorFailed_ThrowsConflictException(
        WarehouseRebuildStatus status
    )
    {
        // Arrange
        WarehouseRebuild rebuild = RebuildIn(status);

        // Act
        Action resume = () => rebuild.Resume(Now);

        // Assert
        resume.Should().Throw<ConflictException>();
    }

    [Fact]
    public void Complete_Running_ClearsTheCursor()
    {
        // Arrange
        WarehouseRebuild rebuild = RebuildIn(WarehouseRebuildStatus.Running);
        rebuild.RecordBatch(new WarehouseBatchProgress(1, 1, 0, 0, Now, [Guid.NewGuid()]), Now);

        // Act
        rebuild.Complete(Now);

        // Assert
        rebuild.Status.Should().Be(WarehouseRebuildStatus.Completed);
        rebuild.CompletedAt.Should().Be(Now);
        rebuild.CursorEventIds.Should().BeEmpty();
        rebuild.IsActive.Should().BeFalse();
    }

    private static WarehouseRebuild RebuildIn(WarehouseRebuildStatus status)
    {
        WarehouseRebuild rebuild = WarehouseRebuild.Request(Now);
        if (status is WarehouseRebuildStatus.Pending)
        {
            return rebuild;
        }

        rebuild.Start(Now);
        switch (status)
        {
            case WarehouseRebuildStatus.Completed:
                rebuild.Complete(Now);
                break;
            case WarehouseRebuildStatus.Interrupted:
                rebuild.Interrupt(Now);
                break;
            case WarehouseRebuildStatus.Failed:
                rebuild.Fail("boom", Now);
                break;
        }

        return rebuild;
    }
}
