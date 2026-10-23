using Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;

namespace Leaderboard.Presentation.DTO;

public sealed record PlayerStatisticsDto(
    string PlayerId,
    long TotalActions,
    decimal ValueTotal,
    DateTimeOffset LastActionAt,
    DateTimeOffset ConsolidatedUntil,
    IReadOnlyList<PlayerActionStatisticsDto> Actions
)
{
    public static PlayerStatisticsDto From(GetPlayerStatisticsResult result) =>
        new(
            result.PlayerId,
            result.TotalActions,
            result.ValueTotal,
            result.LastActionAt,
            result.ConsolidatedUntil,
            [
                .. result.Actions.Select(action => new PlayerActionStatisticsDto(
                    action.ActionType,
                    action.ActionCount,
                    action.ValueTotal
                )),
            ]
        );
}

public sealed record PlayerActionStatisticsDto(
    string ActionType,
    long ActionCount,
    decimal ValueTotal
);
