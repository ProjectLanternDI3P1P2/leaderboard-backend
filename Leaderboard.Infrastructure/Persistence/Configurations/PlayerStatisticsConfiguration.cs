using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public class PlayerStatisticsConfiguration : IEntityTypeConfiguration<PlayerStatistics>
{
    public const string TableName = "warehouse_player_statistics";

    public void Configure(EntityTypeBuilder<PlayerStatistics> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(s => new { s.GenerationId, s.PlayerId });

        builder.Property(s => s.GenerationId).HasColumnName("generation_id");
        // "C" collation: the league table tie-break is a byte order, the same on every server.
        builder
            .Property(s => s.PlayerId)
            .HasColumnName("player_id")
            .HasMaxLength(128)
            .UseCollation("C");
        builder.Property(s => s.TotalActions).HasColumnName("total_actions");
        // Unbounded numeric: exact sums, independent of batch boundaries.
        builder.Property(s => s.ValueTotal).HasColumnName("value_total").HasColumnType("numeric");
        builder.Property(s => s.LastActionAt).HasColumnName("last_action_at");

        // League table: rank order of one generation without a sort.
        builder
            .HasIndex(s => new
            {
                s.GenerationId,
                s.TotalActions,
                s.PlayerId,
            })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_warehouse_player_statistics_rank");
    }
}
