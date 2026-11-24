using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using MediatR;

namespace Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;

public sealed class GetLeagueTableQueryHandler(IWarehouseReadRepository warehouse)
    : IRequestHandler<GetLeagueTableQuery, GetLeagueTableResult>
{
    public async Task<GetLeagueTableResult> Handle(
        GetLeagueTableQuery request,
        CancellationToken cancellationToken
    )
    {
        WarehouseState? state = await warehouse.GetStateAsync(cancellationToken);
        if (state?.ActiveGenerationId is not { } generationId)
        {
            // Nothing consolidated yet: an empty table, not an error.
            return new GetLeagueTableResult(request.Page, request.PageSize, 0, null, []);
        }

        IReadOnlyList<PlayerStatistics> players = await warehouse.GetLeagueTableAsync(
            generationId,
            request.Page,
            request.PageSize,
            cancellationToken
        );
        long totalPlayers = await warehouse.CountPlayersAsync(generationId, cancellationToken);
        long firstRank = ((long)request.Page - 1) * request.PageSize + 1;

        return new GetLeagueTableResult(
            request.Page,
            request.PageSize,
            totalPlayers,
            state.ConsolidatedUntil,
            [
                .. players.Select(
                    (player, index) =>
                        new LeagueTableEntryResult(
                            firstRank + index,
                            player.PlayerId,
                            player.TotalActions,
                            player.LastActionAt
                        )
                ),
            ]
        );
    }
}
