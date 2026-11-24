using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroRankingConfiguration : IEntityTypeConfiguration<HeroRanking>
{
    public void Configure(EntityTypeBuilder<HeroRanking> builder)
    {
        builder.ToTable("hero_rankings");
        builder.HasKey(hero => hero.HeroId);
        builder.Property(hero => hero.HeroId).HasMaxLength(128);
        builder.Property(hero => hero.HeroName).HasMaxLength(128);
        builder.Property(hero => hero.HeroClass).HasMaxLength(32);
        builder.Property(hero => hero.PlayerId).HasMaxLength(128);
        builder.Property(hero => hero.PlayerName).HasMaxLength(128);
        builder.HasIndex(hero => hero.AllTimeXp);
        builder.HasIndex(hero => hero.PlayerId);
    }
}
