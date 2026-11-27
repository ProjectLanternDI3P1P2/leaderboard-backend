using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Test.HeroRanking;

public sealed class HeroRankingQueriesTests
{
    [Fact]
    public async Task Public_ranking_pages_fifty_heroes_with_stable_ties_and_no_gaps()
    {
        await using LeaderboardDbContext database = CreateDatabase();
        database.HeroRankings.AddRange(
            Enumerable
                .Range(1, 110)
                .Reverse()
                .Select(index => Hero($"hero-{index:000}", "warrior", "owner", 1000, null, null))
        );
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var queries = new HeroRankingQueries(database);
        var pages = new[]
        {
            await queries.GetPublicAsync(1, 50, TestContext.Current.CancellationToken),
            await queries.GetPublicAsync(2, 50, TestContext.Current.CancellationToken),
            await queries.GetPublicAsync(3, 50, TestContext.Current.CancellationToken),
        };

        Assert.All(pages, page => Assert.Equal(110, page.TotalCount));
        Assert.Equal([50, 50, 10], pages.Select(page => page.Entries.Count));
        Assert.Equal(
            Enumerable.Range(1, 110).Select(index => $"hero-{index:000}"),
            pages.SelectMany(page => page.Entries).Select(hero => hero.HeroId)
        );
        Assert.Equal(
            Enumerable.Range(1, 110),
            pages.SelectMany(page => page.Entries).Select(hero => hero.Rank)
        );
    }

    [Fact]
    public async Task Empty_public_ranking_returns_an_empty_page()
    {
        await using LeaderboardDbContext database = CreateDatabase();

        var result = await new HeroRankingQueries(database).GetPublicAsync(
            1,
            50,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task Ranking_keeps_xp_rank_when_filtered_and_highlights_authenticated_player()
    {
        await using LeaderboardDbContext database = CreateDatabase();
        database.HeroRankings.AddRange(
            Hero("first", "warrior", "other", 300, 20, 10),
            Hero("second", "mage", "demo-sub", 200, 40, 5),
            Hero("third", "mage", "other", 100, null, null)
        );
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var queries = new HeroRankingQueries(database);
        var result = await queries.GetPageAsync(
            "demo-sub",
            "mage",
            "all-time",
            null,
            "level",
            1,
            10,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([2, 3], result.Entries.Select(entry => entry.Rank));
        Assert.Single(result.Me);
        Assert.Equal("me", result.Me[0].PlayerId);
        Assert.Equal("second", result.Me[0].HeroId);

        var anotherAccount = await queries.GetPageAsync(
            "another-sub",
            "mage",
            "all-time",
            null,
            "level",
            1,
            10,
            TestContext.Current.CancellationToken
        );
        Assert.Empty(anotherAccount.Me);

        var season = await queries.GetPageAsync(
            "demo-sub",
            null,
            "season",
            null,
            "total-xp",
            1,
            10,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, season.TotalCount);
        Assert.DoesNotContain(season.Entries, entry => entry.HeroId == "third");
    }

    [Fact]
    public async Task Public_ranking_and_hero_endpoints_read_the_same_backend_rows()
    {
        await using LeaderboardDbContext database = CreateDatabase();
        database.HeroRankings.Add(Hero("first", "warrior", "me", 300, 20, 10));
        database.HeroStatisticValues.Add(
            new HeroStatisticValue
            {
                HeroId = "first",
                Key = "games_played",
                Value = 99,
                Origin = HeroStatisticValue.DevelopmentSeedOrigin,
            }
        );
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var queries = new HeroRankingQueries(database);
        var publicRanking = await queries.GetPublicAsync(
            1,
            50,
            TestContext.Current.CancellationToken
        );
        var detail = await queries.GetDetailAsync("first", TestContext.Current.CancellationToken);
        var facts = await queries.GetFactsAsync("first", TestContext.Current.CancellationToken);
        var statistics = await queries.GetStatisticsAsync(
            "first",
            TestContext.Current.CancellationToken
        );

        Assert.Equal("first", Assert.Single(publicRanking.Top).HeroId);
        Assert.Equal("first", Assert.Single(publicRanking.Entries).HeroId);
        Assert.Equal("first", detail?.HeroId);
        Assert.Equal(300, facts?.XpFromFacts);
        Assert.Equal(99, statistics.Statistics.Single(item => item.Key == "games_played").Value);
        Assert.True(statistics.IsDemo);
    }

    private static LeaderboardDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<LeaderboardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeaderboardDbContext(options);
    }

    private static Leaderboard.Domain.Entities.HeroRanking Hero(
        string id,
        string heroClass,
        string playerId,
        long xp,
        long? seasonXp,
        long? weekXp
    ) =>
        new()
        {
            HeroId = id,
            HeroName = id,
            HeroClass = heroClass,
            PlayerId = playerId,
            PlayerName = playerId,
            AllTimeXp = xp,
            SeasonXp = seasonXp,
            WeekXp = weekXp,
            Level = (int)(xp / 100),
            Runs = 4,
            WinRate = 0.5,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            SeededAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
}
