using Leaderboard.Application.Features.HeroRanking;
using Leaderboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Infrastructure.Persistence.Repositories;

/// <summary>Reads the backend-owned hero projection. Development seeds this table once.</summary>
public sealed class HeroRankingQueries(LeaderboardDbContext dbContext) : IHeroRankingQueries
{
    private static readonly RankingStatusDetail LiveDetail = new();

    public async Task<HeroRankingPage> GetPageAsync(
        string playerId,
        string? heroClass,
        string period,
        string? search,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        List<HeroRanking> heroes = await ReadAllAsync(cancellationToken);
        List<HeroRankingEntry> matching = Rank(heroes, period)
            .Where(entry => heroClass is null || entry.HeroClass == heroClass)
            .Where(entry =>
                string.IsNullOrWhiteSpace(search)
                || entry.HeroName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || (
                    entry.PlayerName?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                    ?? false
                )
            )
            .ToList();

        // The authenticated player's identity is represented by the Player route alias.
        matching = matching
            .Select(entry => entry.PlayerId == playerId ? entry with { PlayerId = "me" } : entry)
            .ToList();

        IEnumerable<HeroRankingEntry> sorted = sort switch
        {
            "level" => matching.OrderByDescending(entry => entry.Level).ThenBy(entry => entry.Rank),
            "win-rate" => matching
                .OrderByDescending(entry => entry.WinRate)
                .ThenBy(entry => entry.Rank),
            "last-activity" => matching
                .OrderByDescending(entry => entry.LastActivityAt)
                .ThenBy(entry => entry.Rank),
            _ => matching.OrderBy(entry => entry.Rank),
        };

        long offset = ((long)page - 1) * pageSize;
        return new HeroRankingPage(
            ConsolidatedAt(heroes),
            "live",
            LiveDetail,
            Season(heroes),
            page,
            pageSize,
            matching.Count,
            sorted.Skip((int)Math.Min(offset, int.MaxValue)).Take(pageSize).ToList(),
            matching.Where(entry => entry.PlayerId == "me").ToList()
        );
    }

    public async Task<PublicHeroRanking> GetPublicAsync(CancellationToken cancellationToken)
    {
        List<HeroRanking> heroes = await ReadAllAsync(cancellationToken);
        List<HeroRankingEntry> entries = Rank(heroes, "all-time");
        return new PublicHeroRanking(
            ConsolidatedAt(heroes),
            Season(heroes),
            entries.Count,
            entries.Take(3).ToList(),
            [
                new("warrior", entries.FirstOrDefault(entry => entry.HeroClass == "warrior")),
                new("shaman", entries.FirstOrDefault(entry => entry.HeroClass == "shaman")),
                new("mage", entries.FirstOrDefault(entry => entry.HeroClass == "mage")),
            ],
            entries.Take(10).ToList()
        );
    }

    public async Task<HeroRankingDetail?> GetDetailAsync(
        string heroId,
        CancellationToken cancellationToken
    )
    {
        HeroRanking? hero = await FindAsync(heroId, cancellationToken);
        if (hero is null)
            return null;

        int bossesDefeated = (int)Math.Floor(hero.Runs * hero.WinRate);
        var runs = new List<HeroRun>();
        if (hero.LastActivityAt is { } activity)
        {
            for (int index = 0; index < Math.Min(4, hero.Runs); index++)
            {
                int cycle = (hero.Runs + index) % 5;
                string outcome =
                    cycle < 3 ? "victory"
                    : cycle == 3 ? "defeat"
                    : "abandoned";
                int room =
                    outcome == "victory" ? 12
                    : outcome == "defeat" ? 7
                    : 3;
                runs.Add(
                    new HeroRun(
                        outcome,
                        room,
                        12,
                        outcome == "victory" ? "The Hollow King" : null,
                        outcome == "victory" ? 1200
                            : outcome == "defeat" ? 400
                            : 0,
                        activity.AddHours(-index * 4)
                    )
                );
            }
        }

        return new HeroRankingDetail(
            hero.HeroId,
            hero.HeroName,
            hero.HeroClass,
            hero.Level,
            hero.Level / 30,
            hero.CreatedAt,
            hero.Level >= 60 ? 25_000 : hero.AllTimeXp % 25_000,
            25_000,
            runs,
            bossesDefeated,
            hero.Runs == 0 ? 0
                : bossesDefeated > 0 ? 12
                : 3,
            hero.PlayerId
        );
    }

    public async Task<HeroRankingFacts?> GetFactsAsync(
        string heroId,
        CancellationToken cancellationToken
    )
    {
        HeroRanking? hero = await FindAsync(heroId, cancellationToken);
        if (hero is null)
            return null;

        DateTimeOffset latest = hero.LastActivityAt ?? hero.CreatedAt;
        List<HeroFact> facts = Enumerable
            .Range(0, 6)
            .Select(index =>
            {
                string status = index == 0 && hero.IsPending ? "pending" : "applied";
                return new HeroFact(
                    $"{hero.HeroId}-fact-{6 - index}",
                    hero.Runs == 0 ? "xp.imported" : "run.completed",
                    latest.AddMinutes(-47 * index),
                    status,
                    150 + index * 50,
                    status == "pending" ? "Waiting for the next consolidation." : null
                );
            })
            .ToList();
        return new HeroRankingFacts(
            hero.HeroId,
            hero.AllTimeXp,
            $"demo:{hero.HeroId}:{facts[0].FactId}",
            facts
        );
    }

    public async Task<HeroStatistics> GetStatisticsAsync(
        string heroId,
        CancellationToken cancellationToken
    )
    {
        HeroRanking? hero = await FindAsync(heroId, cancellationToken);
        if (hero is null)
        {
            return new HeroStatistics(heroId, null, false, false, []);
        }

        List<HeroStatisticValue> values = await dbContext
            .HeroStatisticValues.AsNoTracking()
            .Where(value => value.HeroId == heroId)
            .OrderBy(value => value.Key)
            .ToListAsync(cancellationToken);
        return new HeroStatistics(
            heroId,
            hero.SeededAt,
            false,
            values.Any(value => value.Origin == HeroStatisticValue.DevelopmentSeedOrigin),
            values.Select(value => new HeroStatistic(value.Key, value.Value)).ToList()
        );
    }

    private Task<HeroRanking?> FindAsync(string heroId, CancellationToken cancellationToken) =>
        dbContext
            .HeroRankings.AsNoTracking()
            .SingleOrDefaultAsync(hero => hero.HeroId == heroId, cancellationToken);

    private Task<List<HeroRanking>> ReadAllAsync(CancellationToken cancellationToken) =>
        dbContext.HeroRankings.AsNoTracking().ToListAsync(cancellationToken);

    private static DateTimeOffset? ConsolidatedAt(List<HeroRanking> heroes) =>
        heroes.Count == 0 ? null : heroes.Max(hero => hero.SeededAt);

    private static RankingSeason Season(List<HeroRanking> heroes) =>
        new(
            "season-3",
            "Season 3",
            (ConsolidatedAt(heroes) ?? DateTimeOffset.UtcNow).AddDays(-30),
            "season-2"
        );

    private static List<HeroRankingEntry> Rank(List<HeroRanking> heroes, string period) =>
        heroes
            .Select(hero => new
            {
                Hero = hero,
                Xp = period switch
                {
                    "season" => hero.SeasonXp,
                    "week" => hero.WeekXp,
                    _ => hero.AllTimeXp,
                },
            })
            .Where(item => item.Xp is not null)
            .OrderByDescending(item => item.Xp)
            .ThenBy(item => item.Hero.HeroId, StringComparer.Ordinal)
            .Select(
                (item, index) =>
                    new HeroRankingEntry(
                        index + 1,
                        item.Hero.RankDelta,
                        item.Hero.HeroId,
                        item.Hero.HeroName,
                        item.Hero.HeroClass,
                        item.Hero.Level,
                        item.Xp!.Value,
                        period == "all-time" || item.Hero.Runs == 0 || item.Hero.AllTimeXp == 0
                            ? item.Hero.Runs
                            : Math.Max(
                                1,
                                (int)
                                    Math.Round(
                                        item.Hero.Runs
                                            * (double)item.Xp!.Value
                                            / item.Hero.AllTimeXp
                                    )
                            ),
                        item.Hero.WinRate,
                        item.Hero.LastActivityAt,
                        item.Hero.PlayerId,
                        item.Hero.PlayerName,
                        item.Hero.IsPending
                    )
            )
            .ToList();
}
