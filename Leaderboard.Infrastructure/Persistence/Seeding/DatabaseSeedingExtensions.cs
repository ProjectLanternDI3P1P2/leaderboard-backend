using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Infrastructure.Persistence.Seeding;

public static class DatabaseSeedingExtensions
{
    public static async Task MigrateDevelopmentDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        LeaderboardDbContext context =
            scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();
        IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        string demoPlayerId =
            configuration["DevelopmentSeed:PlayerId"]
            ?? throw new InvalidOperationException("DevelopmentSeed:PlayerId is required.");
        string demoPlayerName = configuration["DevelopmentSeed:PlayerName"] ?? "demo";

        await context.Database.MigrateAsync(cancellationToken);
        await SeedDevelopmentHeroesAsync(context, demoPlayerId, demoPlayerName, cancellationToken);
    }

    private static async Task SeedDevelopmentHeroesAsync(
        LeaderboardDbContext context,
        string demoPlayerId,
        string demoPlayerName,
        CancellationToken cancellationToken
    )
    {
        // Repair rows created by the first development seed, which used the
        // route alias as a database owner and attributed them to every account.
        List<HeroRanking> legacyOwned = await context
            .HeroRankings.Where(hero =>
                hero.PlayerId == "me"
                && (
                    hero.HeroId == "demo-hero-007"
                    || hero.HeroId == "demo-hero-058"
                    || hero.HeroId == "demo-hero-210"
                )
            )
            .ToListAsync(cancellationToken);
        foreach (HeroRanking hero in legacyOwned)
        {
            hero.PlayerId = demoPlayerId;
            hero.PlayerName = demoPlayerName;
        }
        if (legacyOwned.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        if (await context.HeroRankings.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string[] names =
        [
            "Aldric",
            "Brenna",
            "Corvin",
            "Dahlia",
            "Eamon",
            "Fenna",
            "Garrick",
            "Hestia",
        ];
        string[] classes = ["warrior", "shaman", "mage"];
        var heroes = new List<HeroRanking>(220);

        for (int rank = 1; rank <= 220; rank++)
        {
            bool mine = rank is 7 or 58 or 210;
            bool deleted = rank is 9 or 73 or 150;
            bool inSeason = rank != 210 && rank % 11 is not (1 or 4 or 6 or 9 or 10);
            long xp = 1_500_000 - (rank - 1) * 6_500 + (rank * 7919) % 3000;
            double seasonShare = rank == 7 ? 0.7 : 0.25 + (rank * 37 % 50) / 100.0;
            DateTimeOffset? lastActivity =
                rank == 199 ? null : now.AddMinutes(-((rank * 7919 % (13 * 24 * 60)) + 1));
            if (!inSeason && lastActivity is not null)
            {
                lastActivity = now.AddDays(-(35 + rank * 13 % 120));
            }

            long? seasonXp = inSeason ? (long)Math.Round(xp * seasonShare) : null;
            bool inWeek = inSeason && lastActivity > now.AddDays(-7);
            heroes.Add(
                new HeroRanking
                {
                    HeroId = $"demo-hero-{rank:000}",
                    HeroName = rank switch
                    {
                        7 => "Maelis the Warden",
                        58 => "Ysolde Stormcall",
                        210 => "Quillon Ashveil",
                        _ => $"{names[rank % names.Length]} of the Lantern",
                    },
                    HeroClass = rank switch
                    {
                        7 => "warrior",
                        58 => "shaman",
                        210 => "mage",
                        _ => classes[(rank * 7 + 1) % classes.Length],
                    },
                    PlayerId =
                        deleted ? null
                        : mine ? demoPlayerId
                        : $"demo-player-{rank % 70:000}",
                    PlayerName =
                        deleted ? null
                        : mine ? demoPlayerName
                        : names[rank % names.Length],
                    AllTimeXp = xp,
                    SeasonXp = seasonXp,
                    WeekXp = inWeek
                        ? (long)Math.Round(seasonXp!.Value * (0.05 + rank * 53 % 20 / 100.0))
                        : null,
                    Level = Math.Min(60, 1 + (int)(xp / 25_000)),
                    Runs = rank == 199 ? 0 : 30 + rank * 13 % 170,
                    WinRate = rank == 7 ? 0.64 : 0.2 + rank * 29 % 60 / 100.0,
                    LastActivityAt = lastActivity,
                    CreatedAt = now.AddDays(-(inSeason ? 40 + rank % 60 : 200 + rank)),
                    SeededAt = now,
                    IsPending = rank == 7 || rank % 29 == 4,
                    RankDelta =
                        rank == 7 ? 3
                        : rank % 13 == 0 ? null
                        : rank * 31 % 13 - 7,
                }
            );
        }

        context.HeroRankings.AddRange(heroes);
        await context.SaveChangesAsync(cancellationToken);
    }
}
