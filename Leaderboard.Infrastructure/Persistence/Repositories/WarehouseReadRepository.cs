using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence.Repositories;

public sealed class WarehouseReadRepository(LeaderboardDbContext dbContext)
    : IWarehouseReadRepository
{
    public async Task<WarehouseState?> GetStateAsync(CancellationToken cancellationToken)
    {
        return await dbContext
            .WarehouseState.AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.Id == WarehouseState.SingletonId,
                cancellationToken
            );
    }

    public async Task<PlayerStatistics?> GetPlayerStatisticsAsync(
        Guid generationId,
        string playerId,
        CancellationToken cancellationToken
    )
    {
        return await dbContext
            .PlayerStatistics.AsNoTracking()
            .SingleOrDefaultAsync(
                statistics =>
                    statistics.GenerationId == generationId && statistics.PlayerId == playerId,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<PlayerActionStatistics>> GetPlayerActionStatisticsAsync(
        Guid generationId,
        string playerId,
        CancellationToken cancellationToken
    )
    {
        return await dbContext
            .PlayerActionStatistics.AsNoTracking()
            .Where(statistics =>
                statistics.GenerationId == generationId && statistics.PlayerId == playerId
            )
            .OrderBy(statistics => statistics.ActionType)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerStatistics>> GetLeagueTableAsync(
        Guid generationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        return await dbContext
            .PlayerStatistics.AsNoTracking()
            .Where(statistics => statistics.GenerationId == generationId)
            .OrderByDescending(statistics => statistics.TotalActions)
            .ThenBy(statistics => statistics.PlayerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountPlayersAsync(
        Guid generationId,
        CancellationToken cancellationToken
    )
    {
        return await dbContext.PlayerStatistics.LongCountAsync(
            statistics => statistics.GenerationId == generationId,
            cancellationToken
        );
    }
}
