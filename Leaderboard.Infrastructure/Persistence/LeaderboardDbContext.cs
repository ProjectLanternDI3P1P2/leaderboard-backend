using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence;

public class LeaderboardDbContext(DbContextOptions<LeaderboardDbContext> options)
    : DbContext(options)
{
    public virtual DbSet<Player> Players { get; set; }
    public virtual DbSet<WarehouseRebuild> WarehouseRebuilds { get; set; }
    public virtual DbSet<WarehouseState> WarehouseState { get; set; }
    public virtual DbSet<PlayerStatistics> PlayerStatistics { get; set; }
    public virtual DbSet<PlayerActionStatistics> PlayerActionStatistics { get; set; }
    public virtual DbSet<WarehouseProcessedMessage> WarehouseProcessedMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Applique toutes les configurations d'entités automatiquement
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaderboardDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
