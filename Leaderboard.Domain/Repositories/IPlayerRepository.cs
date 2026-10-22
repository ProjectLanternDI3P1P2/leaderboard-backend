using Leaderboard.Domain.Entities;

namespace Leaderboard.Domain.Repositories;

public interface IPlayerRepository
{
    Task<Player?> GetPlayerByIdAsync(Guid playerId, CancellationToken cancellationToken);
    Task AddPlayerAsync(Player player, CancellationToken cancellationToken);
}
