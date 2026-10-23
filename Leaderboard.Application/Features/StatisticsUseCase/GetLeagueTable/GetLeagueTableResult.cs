namespace Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;

public sealed record GetLeagueTableResult(
    int Page,
    int PageSize,
    long TotalPlayers,
    DateTimeOffset? ConsolidatedUntil,
    IReadOnlyList<LeagueTableEntryResult> Entries
);

public sealed record LeagueTableEntryResult(
    long Rank,
    string PlayerId,
    long TotalActions,
    decimal ValueTotal,
    DateTimeOffset LastActionAt
);
