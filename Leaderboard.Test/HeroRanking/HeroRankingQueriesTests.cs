using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leaderboard.Test.HeroRanking;

public sealed class HeroRankingQueriesTests
{
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
    public async Task Ranking_combines_class_period_search_sort_and_pagination()
    {
        await using LeaderboardDbContext database = CreateDatabase();
        var lowLevelMage = Hero("mage-low-level", "mage", "RunePlayer", 800, 400, 400);
        lowLevelMage.Level = 10;
        var highLevelMage = Hero("mage-high-level", "mage", "rune-player", 700, 500, 500);
        highLevelMage.Level = 30;
        highLevelMage.PlayerId = "current-player";
        database.HeroRankings.AddRange(
            Hero("warrior", "warrior", "other", 900, 900, 900),
            lowLevelMage,
            highLevelMage,
            Hero("mage-no-match", "mage", "other", 600, 300, 300),
            Hero("mage-unranked", "mage", "RunePlayer", 100, null, null)
        );
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        var queries = new HeroRankingQueries(database);
        var result = await queries.GetPageAsync(
            "current-player",
            "mage",
            "season",
            "RUNE",
            "level",
            1,
            1,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(2, result.TotalCount);
        var entry = Assert.Single(result.Entries);
        Assert.Equal("mage-high-level", entry.HeroId);
        Assert.Equal(2, entry.Rank);
        Assert.Equal("mage-high-level", Assert.Single(result.Me).HeroId);

        var secondPage = await queries.GetPageAsync(
            "current-player",
            "mage",
            "season",
            "RUNE",
            "level",
            2,
            1,
            TestContext.Current.CancellationToken
        );
        Assert.Equal("mage-low-level", Assert.Single(secondPage.Entries).HeroId);
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
        var publicRanking = await queries.GetPublicAsync(TestContext.Current.CancellationToken);
        var detail = await queries.GetDetailAsync("first", TestContext.Current.CancellationToken);
        var facts = await queries.GetFactsAsync("first", TestContext.Current.CancellationToken);
        var statistics = await queries.GetStatisticsAsync(
            "first",
            TestContext.Current.CancellationToken
        );

        Assert.Equal("first", Assert.Single(publicRanking.Top).HeroId);
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
