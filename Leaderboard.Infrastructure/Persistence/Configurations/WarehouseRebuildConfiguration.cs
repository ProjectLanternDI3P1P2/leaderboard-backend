using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

// snake_case names (MLD convention): the warehouse bulk writes are raw SQL.
public class WarehouseRebuildConfiguration : IEntityTypeConfiguration<WarehouseRebuild>
{
    public const string TableName = "warehouse_rebuilds";

    public void Configure(EntityTypeBuilder<WarehouseRebuild> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(r => r.Id);
        builder.Ignore(r => r.IsActive);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.GenerationId).HasColumnName("generation_id");
        builder
            .Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.ConsolidatedUntil).HasColumnName("consolidated_until");
        builder.Property(r => r.EventsProcessed).HasColumnName("events_processed");
        builder.Property(r => r.EventsApplied).HasColumnName("events_applied");
        builder.Property(r => r.DuplicatesIgnored).HasColumnName("duplicates_ignored");
        builder.Property(r => r.EventsIgnored).HasColumnName("events_ignored");
        builder.Property(r => r.LatestEventReceivedAt).HasColumnName("latest_event_received_at");
        builder.Property(r => r.CursorEventIds).HasColumnName("cursor_event_ids");
        builder.Property(r => r.AttemptCount).HasColumnName("attempt_count");
        builder.Property(r => r.LastError).HasColumnName("last_error").HasMaxLength(1024);
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // xmin: two replicas cannot both start, or both checkpoint, the same rebuild.
        builder.Property<uint>("Version").HasColumnName("xmin").IsRowVersion();

        // At most one pending or running rebuild, whatever the number of API replicas.
        builder
            .HasIndex(r => r.Status)
            .IsUnique()
            .HasFilter(
                $"status IN ('{nameof(WarehouseRebuildStatus.Pending)}', '{nameof(WarehouseRebuildStatus.Running)}')"
            )
            .HasDatabaseName("ux_warehouse_rebuilds_single_active");
    }
}
