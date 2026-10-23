using Leaderboard.Domain.Entities;

namespace Leaderboard.Domain.Repositories;

public interface IWarehouseRebuildRepository
{
    Task AddAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken);

    Task<WarehouseRebuild?> GetByIdAsync(Guid rebuildId, CancellationToken cancellationToken);

    /// <summary>True while a rebuild is pending or running: only one may exist at a time.</summary>
    Task<bool> HasActiveAsync(CancellationToken cancellationToken);
}
