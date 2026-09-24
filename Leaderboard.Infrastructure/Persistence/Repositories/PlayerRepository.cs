using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;

namespace Leaderboard.Infrastructure.Persistence.Repositories;

public sealed class PlayerRepository(LeaderboardDbContext dbContext) : IPlayerRepository
{
    public async Task AddPlayerAsync(Player player, CancellationToken cancellationToken)
    {
        await dbContext.Players.AddAsync(player, cancellationToken);
    }

    public async Task<Player?> GetPlayerByIdAsync(Guid playerId, CancellationToken cancellationToken)
    {
        return await dbContext.Players.FindAsync([playerId], cancellationToken);
    }
}
