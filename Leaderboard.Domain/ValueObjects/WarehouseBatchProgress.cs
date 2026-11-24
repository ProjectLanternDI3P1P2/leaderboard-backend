namespace Leaderboard.Domain.ValueObjects;

/// <summary>What one committed batch of a rebuild read, applied and skipped.</summary>
/// <param name="CursorReceivedAt">Receipt date of the last event read.</param>
/// <param name="CursorEventIds">
/// Every event already read with that exact receipt date. Several events can share a
/// date, so a resumed rebuild restarts at that date and skips only these.
/// </param>
public sealed record WarehouseBatchProgress(
    long EventsProcessed,
    long EventsApplied,
    long DuplicatesIgnored,
    long EventsIgnored,
    DateTimeOffset CursorReceivedAt,
    IReadOnlyCollection<Guid> CursorEventIds
);
