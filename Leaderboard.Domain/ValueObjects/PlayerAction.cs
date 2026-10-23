namespace Leaderboard.Domain.ValueObjects;

/// <summary>A received event, decoded into what the warehouse counts.</summary>
/// <param name="Value">
/// <c>decimal</c>, not <c>double</c>: sums must not depend on batch boundaries, so a
/// resumed rebuild ends with exactly the totals of a rebuild run in one go.
/// </param>
public sealed record PlayerAction(
    Guid MessageId,
    string PlayerId,
    string ActionType,
    decimal Value,
    DateTimeOffset OccurredAt
);
