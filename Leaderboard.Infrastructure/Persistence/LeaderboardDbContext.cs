using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence;

public class LeaderboardDbContext(DbContextOptions<LeaderboardDbContext> options) : DbContext(options)
{
    public virtual DbSet<Player> Players { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Applique toutes les configurations d'entités automatiquement
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaderboardDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
