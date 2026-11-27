using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroRankingSnapshotConfiguration : IEntityTypeConfiguration<HeroRankingSnapshot>
{
    public const string TableName = "warehouse_hero_rankings";

    public void Configure(EntityTypeBuilder<HeroRankingSnapshot> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(hero => new { hero.GenerationId, hero.HeroId });
        builder.Property(hero => hero.GenerationId).HasColumnName("generation_id");
        builder.Property(hero => hero.HeroId).HasColumnName("hero_id").HasMaxLength(128);
        builder.Property(hero => hero.PlayerId).HasColumnName("player_id").HasMaxLength(128);
        builder.Property(hero => hero.HeroClassId).HasColumnName("hero_class_id").HasMaxLength(128);
        builder.Property(hero => hero.TotalXp).HasColumnName("total_xp");
        builder.Property(hero => hero.Level).HasColumnName("level");
        builder.Property(hero => hero.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(hero => hero.LastActivityAt).HasColumnName("last_activity_at");
        builder
            .HasIndex(hero => new
            {
                hero.GenerationId,
                hero.TotalXp,
                hero.HeroId,
            })
            .IsDescending(false, true, false);
    }
}
