namespace Leaderboard.Application.Features.HeroRanking;

public sealed record HeroRankingEntry(
    int Rank,
    int? RankDelta,
    string HeroId,
    string HeroName,
    string HeroClass,
    int Level,
    long TotalXp,
    int Runs,
    double WinRate,
    DateTimeOffset? LastActivityAt,
    string? PlayerId,
    string? PlayerName,
    bool IsPending
);

public sealed record RankingStatusDetail(
    int? LagSeconds = null,
    int? PendingCount = null,
    DateTimeOffset? CorrectUpTo = null,
    double? RebuildProgress = null,
    DateTimeOffset? SnapshotAt = null,
    DateTimeOffset? FrozenAt = null,
    string? Reason = null
);

public sealed record RankingSeason(
    string Id,
    string Label,
    DateTimeOffset OpenedAt,
    string? PreviousSeasonId
);

public sealed record HeroRankingPage(
    DateTimeOffset? ConsolidatedAt,
    string Status,
    RankingStatusDetail StatusDetail,
    RankingSeason Season,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<HeroRankingEntry> Entries,
    IReadOnlyList<HeroRankingEntry> Me
);

public sealed record ClassLeader(string HeroClass, HeroRankingEntry? Entry);

public sealed record PublicHeroRanking(
    DateTimeOffset? ConsolidatedAt,
    RankingSeason Season,
    int TotalCount,
    IReadOnlyList<HeroRankingEntry> Podium,
    IReadOnlyList<ClassLeader> LeadersByClass,
    IReadOnlyList<HeroRankingEntry> Top
);

public sealed record HeroRun(
    string Outcome,
    int Room,
    int MaxRoom,
    string? Boss,
    int Xp,
    DateTimeOffset EndedAt
);

public sealed record HeroRankingDetail(
    string HeroId,
    string HeroName,
    string HeroClass,
    int Level,
    int NgTier,
    DateTimeOffset CreatedAt,
    long XpIntoLevel,
    long XpForNextLevel,
    IReadOnlyList<HeroRun> LastRuns,
    int BossesDefeated,
    int DeepestRoom,
    string? PlayerId
);

public sealed record HeroFact(
    string FactId,
    string Type,
    DateTimeOffset IngestedAt,
    string Status,
    int Xp,
    string? Reason
);

public sealed record HeroRankingFacts(
    string HeroId,
    long XpFromFacts,
    string IdempotencyKey,
    IReadOnlyList<HeroFact> Facts
);

public sealed record HeroStatistic(string Key, long Value);

public sealed record HeroStatistics(
    string HeroId,
    DateTimeOffset? ConsolidatedAt,
    bool IsConsolidating,
    IReadOnlyList<HeroStatistic> Statistics
);

public interface IHeroRankingQueries
{
    Task<HeroRankingPage> GetPageAsync(
        string playerId,
        string? heroClass,
        string period,
        string? search,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<PublicHeroRanking> GetPublicAsync(CancellationToken cancellationToken);
    Task<HeroRankingDetail?> GetDetailAsync(string heroId, CancellationToken cancellationToken);
    Task<HeroRankingFacts?> GetFactsAsync(string heroId, CancellationToken cancellationToken);
    Task<HeroStatistics> GetStatisticsAsync(string heroId, CancellationToken cancellationToken);
}
