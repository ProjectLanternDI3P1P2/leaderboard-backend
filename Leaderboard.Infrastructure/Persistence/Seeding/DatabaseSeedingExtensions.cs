using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Leaderboard.Infrastructure.Persistence.Seeding;

public static class DatabaseSeedingExtensions
{
    private static readonly (int Rank, string HeroId, string Name, int Level)[] DemoPlayerHeroes =
    [
        (7, "00000000-0000-0000-0000-000000000101", "Aldric", 9),
        (58, "00000000-0000-0000-0000-000000000102", "Sahra", 7),
        (210, "00000000-0000-0000-0000-000000000103", "Corvin", 11),
    ];
    private static readonly string[] DemoPlayerHeroIds = DemoPlayerHeroes
        .Select(hero => hero.HeroId)
        .ToArray();

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
        await RepairDevelopmentHeroIdsAsync(
            context,
            demoPlayerId,
            demoPlayerName,
            cancellationToken
        );
        await SeedDevelopmentHeroesAsync(context, demoPlayerId, demoPlayerName, cancellationToken);
        await SeedDevelopmentStatisticsAsync(context, cancellationToken);
    }

    private static async Task RepairDevelopmentHeroIdsAsync(
        LeaderboardDbContext context,
        string demoPlayerId,
        string demoPlayerName,
        CancellationToken cancellationToken
    )
    {
        // Early leaderboard seeds invented hero IDs independently of Player.
        // Replace only those three development rows, keeping their ranking figures.
        var legacyIds = DemoPlayerHeroes.Select(hero => $"demo-hero-{hero.Rank:000}").ToArray();
        List<HeroRanking> legacy = await context
            .HeroRankings.Where(hero => legacyIds.Contains(hero.HeroId))
            .ToListAsync(cancellationToken);
        if (legacy.Count == 0)
            return;

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        foreach (HeroRanking old in legacy)
        {
            int rank = int.Parse(old.HeroId[^3..]);
            var playerHero = DemoPlayerHeroes.Single(hero => hero.Rank == rank);
            bool alreadyRepaired = await context.HeroRankings.AnyAsync(
                hero => hero.HeroId == playerHero.HeroId,
                cancellationToken
            );

            List<HeroStatisticValue> oldValues = await context
                .HeroStatisticValues.Where(value => value.HeroId == old.HeroId)
                .ToListAsync(cancellationToken);
            context.HeroStatisticValues.RemoveRange(oldValues);
            context.HeroRankings.Remove(old);
            await context.SaveChangesAsync(cancellationToken);

            if (alreadyRepaired)
                continue;

            context.HeroRankings.Add(
                new HeroRanking
                {
                    HeroId = playerHero.HeroId,
                    HeroName = playerHero.Name,
                    HeroClass = old.HeroClass,
                    PlayerId = demoPlayerId,
                    PlayerName = demoPlayerName,
                    AllTimeXp = old.AllTimeXp,
                    SeasonXp = old.SeasonXp,
                    WeekXp = old.WeekXp,
                    Level = playerHero.Level,
                    Runs = old.Runs,
                    WinRate = old.WinRate,
                    LastActivityAt = old.LastActivityAt,
                    CreatedAt = old.CreatedAt,
                    SeededAt = old.SeededAt,
                    IsPending = old.IsPending,
                    RankDelta = old.RankDelta,
                }
            );
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedDevelopmentHeroesAsync(
        LeaderboardDbContext context,
        string demoPlayerId,
        string demoPlayerName,
        CancellationToken cancellationToken
    )
    {
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
            var demoHero = DemoPlayerHeroes.FirstOrDefault(hero => hero.Rank == rank);
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
                    HeroId = demoHero.HeroId ?? $"demo-hero-{rank:000}",
                    HeroName = demoHero.Name ?? $"{names[rank % names.Length]} of the Lantern",
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
                    Level =
                        demoHero.Level > 0 ? demoHero.Level : Math.Min(60, 1 + (int)(xp / 25_000)),
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

    private static async Task SeedDevelopmentStatisticsAsync(
        LeaderboardDbContext context,
        CancellationToken cancellationToken
    )
    {
        List<HeroRanking> heroes = await context
            .HeroRankings.AsNoTracking()
            .Where(hero =>
                hero.HeroId.StartsWith("demo-hero-") || DemoPlayerHeroIds.Contains(hero.HeroId)
            )
            .ToListAsync(cancellationToken);
        var existing = (
            await context
                .HeroStatisticValues.AsNoTracking()
                .Select(value => new { value.HeroId, value.Key })
                .ToListAsync(cancellationToken)
        )
            .Select(value => (value.HeroId, value.Key))
            .ToHashSet();

        var missing = heroes
            .SelectMany(StatisticValues)
            .Where(value => !existing.Contains((value.HeroId, value.Key)))
            .ToList();
        if (missing.Count == 0)
            return;

        context.HeroStatisticValues.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<HeroStatisticValue> StatisticValues(HeroRanking hero)
    {
        long wins = (long)Math.Floor(hero.Runs * hero.WinRate);
        (string Key, long Value)[] values =
        [
            ("games_played", hero.Runs),
            ("games_completed", hero.Runs),
            ("lobbies_joined", hero.Runs),
            ("dungeons_started", hero.Runs),
            ("dungeons_completed", wins),
            ("rooms_explored", hero.Runs * 8L),
            ("chests_opened", hero.Runs * 2L),
            ("combats_fought", hero.Runs * 5L),
            ("combats_won", wins * 5),
            ("actions_performed", hero.Runs * 25L),
            ("consumables_used", hero.Runs),
            ("rewards_earned", wins * 2),
            ("items_consumed", hero.Runs),
            ("purchases_made", hero.Runs / 3),
            ("equipment_changes", hero.Runs / 2),
        ];
        return values.Select(value => new HeroStatisticValue
        {
            HeroId = hero.HeroId,
            Key = value.Key,
            Value = value.Value,
            Origin = HeroStatisticValue.DevelopmentSeedOrigin,
        });
    }
}
