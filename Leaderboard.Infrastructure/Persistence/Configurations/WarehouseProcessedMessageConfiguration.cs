using Leaderboard.Infrastructure.Persistence.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public class WarehouseProcessedMessageConfiguration
    : IEntityTypeConfiguration<WarehouseProcessedMessage>
{
    public const string TableName = "warehouse_processed_messages";

    public void Configure(EntityTypeBuilder<WarehouseProcessedMessage> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(m => new { m.GenerationId, m.MessageId });

        builder.Property(m => m.GenerationId).HasColumnName("generation_id");
        builder.Property(m => m.MessageId).HasColumnName("message_id");
    }
}
