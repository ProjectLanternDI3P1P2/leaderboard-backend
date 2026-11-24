using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroStatisticValueConfiguration : IEntityTypeConfiguration<HeroStatisticValue>
{
    public void Configure(EntityTypeBuilder<HeroStatisticValue> builder)
    {
        builder.ToTable("hero_statistic_values");
        builder.HasKey(value => new { value.HeroId, value.Key });
        builder.Property(value => value.HeroId).HasMaxLength(128);
        builder.Property(value => value.Key).HasMaxLength(64);
        builder.Property(value => value.Origin).HasMaxLength(32);
        builder
            .HasOne<HeroRanking>()
            .WithMany()
            .HasForeignKey(value => value.HeroId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
