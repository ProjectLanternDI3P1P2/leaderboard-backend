using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroClassConfiguration : IEntityTypeConfiguration<HeroClass>
{
    public void Configure(EntityTypeBuilder<HeroClass> builder)
    {
        builder.ToTable("classes");

        builder.HasKey(heroClass => heroClass.Id);

        builder.Property(heroClass => heroClass.Id)
            .HasColumnName("id");

        builder.Property(heroClass => heroClass.Name)
            .IsRequired()
            .HasColumnName("name")
            .HasColumnType("varchar");

        builder.HasIndex(heroClass => heroClass.Name)
            .IsUnique();
    }
}
