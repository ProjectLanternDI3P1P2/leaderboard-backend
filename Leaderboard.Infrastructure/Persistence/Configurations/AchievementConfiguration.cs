using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class AchievementConfiguration : IEntityTypeConfiguration<Achievement>
{
    public void Configure(EntityTypeBuilder<Achievement> builder)
    {
        builder.ToTable("achievements");

        builder.HasKey(achievement => achievement.Id);

        builder.Property(achievement => achievement.Id)
            .HasColumnName("id");

        builder.Property(achievement => achievement.Name)
            .IsRequired()
            .HasColumnName("name")
            .HasColumnType("varchar");

        builder.Property(achievement => achievement.StatisticId)
            .IsRequired()
            .HasColumnName("id_statistic");

        builder.Property(achievement => achievement.Operator)
            .IsRequired()
            .HasColumnName("operator")
            .HasColumnType("varchar");

        builder.Property(achievement => achievement.Threshold)
            .IsRequired()
            .HasColumnName("threshold");

        builder.Property(achievement => achievement.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.HasOne(achievement => achievement.Statistic)
            .WithMany(statistic => statistic.Achievements)
            .HasForeignKey(achievement => achievement.StatisticId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
