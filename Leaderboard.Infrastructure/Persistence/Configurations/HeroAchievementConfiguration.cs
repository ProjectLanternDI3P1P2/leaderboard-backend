using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroAchievementConfiguration : IEntityTypeConfiguration<HeroAchievement>
{
    public void Configure(EntityTypeBuilder<HeroAchievement> builder)
    {
        builder.ToTable("hero_achievements");

        builder.HasKey(heroAchievement => new { heroAchievement.AchievementId, heroAchievement.HeroId });

        builder.Property(heroAchievement => heroAchievement.AchievementId)
            .HasColumnName("id_achievement");

        builder.Property(heroAchievement => heroAchievement.HeroId)
            .HasColumnName("id_hero");

        builder.Property(heroAchievement => heroAchievement.ObtainedAt)
            .IsRequired()
            .HasColumnName("obtained_at");

        builder.HasOne(heroAchievement => heroAchievement.Achievement)
            .WithMany(achievement => achievement.HeroAchievements)
            .HasForeignKey(heroAchievement => heroAchievement.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(heroAchievement => heroAchievement.Hero)
            .WithMany(hero => hero.HeroAchievements)
            .HasForeignKey(heroAchievement => heroAchievement.HeroId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
