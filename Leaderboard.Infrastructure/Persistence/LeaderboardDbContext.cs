using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence;

public class LeaderboardDbContext(DbContextOptions<LeaderboardDbContext> options) : DbContext(options)
{
    public virtual DbSet<Player> Players { get; set; }
    public virtual DbSet<HeroClass> HeroClasses { get; set; }
    public virtual DbSet<Hero> Heroes { get; set; }
    public virtual DbSet<Statistic> Statistics { get; set; }
    public virtual DbSet<HeroStatistic> HeroStatistics { get; set; }
    public virtual DbSet<Achievement> Achievements { get; set; }
    public virtual DbSet<HeroAchievement> HeroAchievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Applique toutes les configurations d'entités automatiquement
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaderboardDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
