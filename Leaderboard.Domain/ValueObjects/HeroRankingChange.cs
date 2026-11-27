namespace Leaderboard.Domain.ValueObjects;

/// <summary>A hero fact decoded from one validated, deduplicated game event.</summary>
public sealed record HeroRankingChange(
    string HeroId,
    string PlayerId,
    string? HeroClassId,
    long? TotalXpAfter,
    int? Level,
    DateTimeOffset OccurredAt
);
