namespace Leaderboard.Domain.ValueObjects;

/// <summary>One hero's changes within a deduplicated warehouse batch.</summary>
public sealed record HeroRankingDelta(
    string HeroId,
    string PlayerId,
    string? HeroClassId,
    long? TotalXpAfter,
    int? Level,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastActivityAt
);
