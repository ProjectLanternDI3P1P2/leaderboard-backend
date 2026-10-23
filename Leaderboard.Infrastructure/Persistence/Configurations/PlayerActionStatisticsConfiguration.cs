using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public class PlayerActionStatisticsConfiguration : IEntityTypeConfiguration<PlayerActionStatistics>
{
    public const string TableName = "warehouse_player_action_statistics";

    public void Configure(EntityTypeBuilder<PlayerActionStatistics> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(s => new
        {
            s.GenerationId,
            s.PlayerId,
            s.ActionType,
        });

        builder.Property(s => s.GenerationId).HasColumnName("generation_id");
        builder.Property(s => s.PlayerId).HasColumnName("player_id").HasMaxLength(128);
        builder.Property(s => s.ActionType).HasColumnName("action_type").HasMaxLength(128);
        builder.Property(s => s.ActionCount).HasColumnName("action_count");
        builder.Property(s => s.ValueTotal).HasColumnName("value_total").HasColumnType("numeric");
    }
}
