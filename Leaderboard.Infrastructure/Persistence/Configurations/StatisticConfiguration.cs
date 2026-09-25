using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class StatisticConfiguration : IEntityTypeConfiguration<Statistic>
{
    public void Configure(EntityTypeBuilder<Statistic> builder)
    {
        builder.ToTable("statistics");

        builder.HasKey(statistic => statistic.Id);

        builder.Property(statistic => statistic.Id)
            .HasColumnName("id");

        builder.Property(statistic => statistic.Name)
            .IsRequired()
            .HasColumnName("name")
            .HasColumnType("varchar");

        builder.Property(statistic => statistic.CountsInScore)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("counts_in_score");

        builder.Property(statistic => statistic.Multiplier)
            .IsRequired()
            .HasDefaultValue(1)
            .HasColumnName("multiplier");

        builder.Property(statistic => statistic.IsSortable)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_sortable");

        builder.Property(statistic => statistic.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.HasIndex(statistic => statistic.Name)
            .IsUnique();
    }
}
