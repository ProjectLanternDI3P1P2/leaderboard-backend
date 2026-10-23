namespace Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;

public sealed record GetPlayerStatisticsResult(
    string PlayerId,
    long TotalActions,
    decimal ValueTotal,
    DateTimeOffset LastActionAt,
    DateTimeOffset ConsolidatedUntil,
    IReadOnlyList<PlayerActionStatisticsResult> Actions
);

public sealed record PlayerActionStatisticsResult(
    string ActionType,
    long ActionCount,
    decimal ValueTotal
);
