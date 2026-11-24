using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence.Repositories;

public sealed class WarehouseRebuildRepository(LeaderboardDbContext dbContext)
    : IWarehouseRebuildRepository
{
    public async Task AddAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken)
    {
        await dbContext.WarehouseRebuilds.AddAsync(rebuild, cancellationToken);
    }

    public async Task<WarehouseRebuild?> GetByIdAsync(
        Guid rebuildId,
        CancellationToken cancellationToken
    )
    {
        return await dbContext.WarehouseRebuilds.FindAsync([rebuildId], cancellationToken);
    }

    public async Task<bool> HasActiveAsync(CancellationToken cancellationToken)
    {
        return await dbContext.WarehouseRebuilds.AnyAsync(
            rebuild =>
                rebuild.Status == WarehouseRebuildStatus.Pending
                || rebuild.Status == WarehouseRebuildStatus.Running,
            cancellationToken
        );
    }
}
