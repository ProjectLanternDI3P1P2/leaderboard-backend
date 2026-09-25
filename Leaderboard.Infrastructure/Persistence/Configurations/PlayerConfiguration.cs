using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players", table => table.ExcludeFromMigrations());

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .IsRequired()
            .HasColumnName("PlayerId");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("PlayerName");

        builder.Property(p => p.Health)
            .IsRequired()
            .HasColumnName("PlayerHealth");

        builder.Property(p => p.MaxHealth)
            .IsRequired()
            .HasColumnName("PlayerMaxHealth");

        builder.Property(p => p.Attack)
            .IsRequired()
            .HasColumnName("PlayerAttack");
    }
}
