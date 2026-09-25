using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Leaderboard.Infrastructure.Persistence.Configurations;

public sealed class HeroConfiguration : IEntityTypeConfiguration<Hero>
{
    public void Configure(EntityTypeBuilder<Hero> builder)
    {
        builder.ToTable("heroes");

        builder.HasKey(hero => hero.Id);

        builder.Property(hero => hero.Id)
            .HasColumnName("id");

        builder.Property(hero => hero.PlayerId)
            .HasColumnName("id_player");

        builder.Property(hero => hero.ClassId)
            .IsRequired()
            .HasColumnName("id_class");

        builder.Property(hero => hero.Pseudo)
            .IsRequired()
            .HasColumnName("pseudo")
            .HasColumnType("varchar");

        builder.Property(hero => hero.Level)
            .IsRequired()
            .HasColumnName("level");

        builder.Property(hero => hero.Score)
            .IsRequired()
            .HasDefaultValue(0)
            .HasColumnName("score");

        builder.Property(hero => hero.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.HasOne(hero => hero.Class)
            .WithMany(heroClass => heroClass.Heroes)
            .HasForeignKey(hero => hero.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(hero => hero.PlayerId);
        builder.HasIndex(hero => new { hero.ClassId, hero.Score });
        builder.HasIndex(hero => hero.Score);
    }
}
