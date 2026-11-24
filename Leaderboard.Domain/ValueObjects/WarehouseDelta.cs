namespace Leaderboard.Domain.ValueObjects;

/// <summary>Increments to add to the warehouse for one batch of new actions.</summary>
public sealed record WarehouseDelta(
    IReadOnlyList<PlayerStatisticsDelta> Players,
    IReadOnlyList<PlayerActionStatisticsDelta> Actions
);

public sealed record PlayerStatisticsDelta(
    string PlayerId,
    long TotalActions,
    DateTimeOffset LastActionAt
);

public sealed record PlayerActionStatisticsDelta(
    string PlayerId,
    string ActionType,
    long ActionCount,
    decimal ValueTotal
);
