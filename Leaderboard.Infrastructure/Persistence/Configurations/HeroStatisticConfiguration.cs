using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroStatisticConfiguration : IEntityTypeConfiguration<HeroStatistic>
{
    public void Configure(EntityTypeBuilder<HeroStatistic> builder)
    {
        builder.ToTable("hero_statistics");

        builder.HasKey(heroStatistic => new { heroStatistic.HeroId, heroStatistic.StatisticId });

        builder.Property(heroStatistic => heroStatistic.HeroId)
            .HasColumnName("id_hero");

        builder.Property(heroStatistic => heroStatistic.StatisticId)
            .HasColumnName("id_statistic");

        builder.Property(heroStatistic => heroStatistic.Value)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("value");

        builder.Property(heroStatistic => heroStatistic.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.HasOne(heroStatistic => heroStatistic.Hero)
            .WithMany(hero => hero.HeroStatistics)
            .HasForeignKey(heroStatistic => heroStatistic.HeroId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(heroStatistic => heroStatistic.Statistic)
            .WithMany(statistic => statistic.HeroStatistics)
            .HasForeignKey(heroStatistic => heroStatistic.StatisticId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(heroStatistic => new { heroStatistic.StatisticId, heroStatistic.Value });
    }
}
