using Leaderboard.Domain.Entities;

namespace Leaderboard.Domain.Repositories;

/// <summary>Reads the active generation only, so a rebuild in progress is never visible.</summary>
public interface IWarehouseReadRepository
{
    Task<WarehouseState?> GetStateAsync(CancellationToken cancellationToken);

    Task<PlayerStatistics?> GetPlayerStatisticsAsync(
        Guid generationId,
        string playerId,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyList<PlayerActionStatistics>> GetPlayerActionStatisticsAsync(
        Guid generationId,
        string playerId,
        CancellationToken cancellationToken
    );

    /// <summary>Players ordered by rank: total actions descending, then player ID.</summary>
    Task<IReadOnlyList<PlayerStatistics>> GetLeagueTableAsync(
        Guid generationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );

    Task<long> CountPlayersAsync(Guid generationId, CancellationToken cancellationToken);
}
