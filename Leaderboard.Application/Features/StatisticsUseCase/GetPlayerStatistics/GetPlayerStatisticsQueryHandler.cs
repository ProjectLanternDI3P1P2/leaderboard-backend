using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using MediatR;

namespace Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;

public sealed class GetPlayerStatisticsQueryHandler(IWarehouseReadRepository warehouse)
    : IRequestHandler<GetPlayerStatisticsQuery, GetPlayerStatisticsResult>
{
    public async Task<GetPlayerStatisticsResult> Handle(
        GetPlayerStatisticsQuery request,
        CancellationToken cancellationToken
    )
    {
        WarehouseState? state = await warehouse.GetStateAsync(cancellationToken);
        PlayerStatistics? statistics = state?.ActiveGenerationId is { } generationId
            ? await warehouse.GetPlayerStatisticsAsync(
                generationId,
                request.PlayerId,
                cancellationToken
            )
            : null;

        if (statistics is null || state?.ConsolidatedUntil is not { } consolidatedUntil)
        {
            throw new KeyNotFoundException(
                $"No statistics found for PlayerId '{request.PlayerId}'."
            );
        }

        IReadOnlyList<PlayerActionStatistics> actions =
            await warehouse.GetPlayerActionStatisticsAsync(
                statistics.GenerationId,
                request.PlayerId,
                cancellationToken
            );

        return new GetPlayerStatisticsResult(
            statistics.PlayerId,
            statistics.TotalActions,
            statistics.LastActionAt,
            consolidatedUntil,
            [
                .. actions.Select(action => new PlayerActionStatisticsResult(
                    action.ActionType,
                    action.ActionCount,
                    action.ValueTotal
                )),
            ]
        );
    }
}
