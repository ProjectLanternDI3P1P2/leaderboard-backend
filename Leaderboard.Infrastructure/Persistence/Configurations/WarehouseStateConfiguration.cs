using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public class WarehouseStateConfiguration : IEntityTypeConfiguration<WarehouseState>
{
    public void Configure(EntityTypeBuilder<WarehouseState> builder)
    {
        builder.ToTable("warehouse_state");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(s => s.ActiveGenerationId).HasColumnName("active_generation_id");
        builder.Property(s => s.PreviousGenerationId).HasColumnName("previous_generation_id");
        builder.Property(s => s.ActivatedAt).HasColumnName("activated_at");
        builder.Property(s => s.ConsolidatedUntil).HasColumnName("consolidated_until");

        builder.HasData(new WarehouseState { Id = WarehouseState.SingletonId });
    }
}
