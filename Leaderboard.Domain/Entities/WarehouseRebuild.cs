using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Exceptions;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Domain.Entities;

/// <summary>
/// One full recalculation of the warehouse from the raw event store, written into its
/// own generation and checkpointed after every batch so that it can be resumed.
/// </summary>
public class WarehouseRebuild
{
    private WarehouseRebuild() { }

    public Guid Id { get; private set; }

    /// <summary>The warehouse generation this rebuild writes; activated once complete.</summary>
    public Guid GenerationId { get; private set; }

    public WarehouseRebuildStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Exclusive upper bound on receipt dates, fixed at request time: a resumed rebuild
    /// reads exactly the same events as an uninterrupted one.
    /// </summary>
    public DateTimeOffset ConsolidatedUntil { get; private set; }

    /// <summary>Raw events read from the store, duplicates and skipped ones included.</summary>
    public long EventsProcessed { get; private set; }

    /// <summary>Distinct events counted into the warehouse.</summary>
    public long EventsApplied { get; private set; }

    /// <summary>Events whose message ID was already counted (redeliveries).</summary>
    public long DuplicatesIgnored { get; private set; }

    /// <summary>Events that cannot be decoded or name no player.</summary>
    public long EventsIgnored { get; private set; }

    /// <summary>Receipt date of the most recent event read.</summary>
    public DateTimeOffset? LatestEventReceivedAt { get; private set; }

    public List<Guid> CursorEventIds { get; private set; } = [];
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Heartbeat: refreshed by every batch of a running rebuild.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive =>
        Status is WarehouseRebuildStatus.Pending or WarehouseRebuildStatus.Running;

    public static WarehouseRebuild Request(DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            GenerationId = Guid.NewGuid(),
            Status = WarehouseRebuildStatus.Pending,
            RequestedAt = now,
            ConsolidatedUntil = now,
            UpdatedAt = now,
        };

    public void Resume(DateTimeOffset now)
    {
        if (Status is not (WarehouseRebuildStatus.Interrupted or WarehouseRebuildStatus.Failed))
        {
            throw new ConflictException(
                $"Warehouse rebuild '{Id}' is {Status}; only an interrupted or failed rebuild can be resumed."
            );
        }

        Status = WarehouseRebuildStatus.Pending;
        LastError = null;
        UpdatedAt = now;
    }

    public void Start(DateTimeOffset now)
    {
        if (Status is not WarehouseRebuildStatus.Pending)
        {
            throw new ConflictException(
                $"Warehouse rebuild '{Id}' is {Status}; only a pending rebuild can start."
            );
        }

        Status = WarehouseRebuildStatus.Running;
        AttemptCount++;
        UpdatedAt = now;
    }

    public void RecordBatch(WarehouseBatchProgress progress, DateTimeOffset now)
    {
        EnsureRunning();

        EventsProcessed += progress.EventsProcessed;
        EventsApplied += progress.EventsApplied;
        DuplicatesIgnored += progress.DuplicatesIgnored;
        EventsIgnored += progress.EventsIgnored;
        LatestEventReceivedAt = progress.CursorReceivedAt;
        CursorEventIds = [.. progress.CursorEventIds];
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        EnsureRunning();

        Status = WarehouseRebuildStatus.Completed;
        CompletedAt = now;
        CursorEventIds = [];
        UpdatedAt = now;
    }

    public void Interrupt(DateTimeOffset now)
    {
        EnsureRunning();

        Status = WarehouseRebuildStatus.Interrupted;
        UpdatedAt = now;
    }

    public void Fail(string error, DateTimeOffset now)
    {
        EnsureRunning();

        Status = WarehouseRebuildStatus.Failed;
        LastError = error;
        UpdatedAt = now;
    }

    private void EnsureRunning()
    {
        if (Status is not WarehouseRebuildStatus.Running)
        {
            throw new ConflictException($"Warehouse rebuild '{Id}' is {Status}, not running.");
        }
    }
}
