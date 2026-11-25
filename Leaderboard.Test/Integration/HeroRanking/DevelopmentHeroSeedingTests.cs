using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Repositories;
using Leaderboard.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using HeroRankingEntity = Leaderboard.Domain.Entities.HeroRanking;

namespace Leaderboard.Test.Integration.HeroRankings;

public sealed class DevelopmentHeroSeedingTests
{
    private const string DemoPlayerId = "00000000-0000-0000-0000-000000000010";
    private const string DemoMageId = "00000000-0000-0000-0000-000000000103";

    [Fact]
    public async Task Development_startup_seeds_Player_hero_ids_and_statistics_once()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(DatabaseName());
        await using ServiceProvider services = CreateServices(database.ConnectionString);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await services.MigrateDevelopmentDatabaseAsync(cancellationToken);
        await services.MigrateDevelopmentDatabaseAsync(cancellationToken);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        LeaderboardDbContext context =
            scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();
        Assert.Equal(220, await context.HeroRankings.CountAsync(cancellationToken));
        Assert.Equal(
            3,
            await context.HeroRankings.CountAsync(
                hero => hero.PlayerId == DemoPlayerId,
                cancellationToken
            )
        );
        Assert.Equal(3300, await context.HeroStatisticValues.CountAsync(cancellationToken));
        Assert.False(
            await context.HeroRankings.AnyAsync(
                hero => hero.HeroId == "demo-hero-007",
                cancellationToken
            )
        );

        HeroRankingEntity? mage = await context.HeroRankings.SingleOrDefaultAsync(
            hero => hero.HeroId == DemoMageId,
            cancellationToken
        );
        Assert.NotNull(mage);
        Assert.Equal("Corvin", mage.HeroName);
        Assert.Equal(11, mage.Level);
        var statistics = await new HeroRankingQueries(context).GetStatisticsAsync(
            DemoMageId,
            cancellationToken
        );
        Assert.True(statistics.IsDemo);
        Assert.Equal(15, statistics.Statistics.Count);
    }

    [Fact]
    public async Task Development_startup_repairs_legacy_demo_hero_without_losing_its_rank()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(DatabaseName());
        await using ServiceProvider services = CreateServices(database.ConnectionString);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            LeaderboardDbContext context =
                scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();
            context.HeroRankings.Add(
                new HeroRankingEntity
                {
                    HeroId = "demo-hero-007",
                    HeroName = "Old seed hero",
                    HeroClass = "warrior",
                    PlayerId = "me",
                    PlayerName = "Old seed player",
                    AllTimeXp = 123_456,
                    Level = 50,
                    Runs = 4,
                    WinRate = 0.5,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    SeededAt = DateTimeOffset.UtcNow,
                }
            );
            context.HeroStatisticValues.Add(
                new HeroStatisticValue
                {
                    HeroId = "demo-hero-007",
                    Key = "games_played",
                    Value = 4,
                    Origin = HeroStatisticValue.DevelopmentSeedOrigin,
                }
            );
            await context.SaveChangesAsync(cancellationToken);
        }

        await services.MigrateDevelopmentDatabaseAsync(cancellationToken);

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            LeaderboardDbContext context =
                scope.ServiceProvider.GetRequiredService<LeaderboardDbContext>();
            Assert.False(
                await context.HeroRankings.AnyAsync(
                    hero => hero.HeroId == "demo-hero-007",
                    cancellationToken
                )
            );
            Assert.False(
                await context.HeroStatisticValues.AnyAsync(
                    value => value.HeroId == "demo-hero-007",
                    cancellationToken
                )
            );
            HeroRankingEntity hero = await context.HeroRankings.SingleAsync(
                item => item.HeroId == "00000000-0000-0000-0000-000000000101",
                cancellationToken
            );
            Assert.Equal(DemoPlayerId, hero.PlayerId);
            Assert.Equal("Aldric", hero.HeroName);
            Assert.Equal(123_456, hero.AllTimeXp);
            Assert.Equal(
                15,
                await context.HeroStatisticValues.CountAsync(
                    value => value.HeroId == hero.HeroId,
                    cancellationToken
                )
            );
        }
    }

    private static string DatabaseName() => $"leaderboard_hero_seed_{Guid.NewGuid():N}";

    private static ServiceProvider CreateServices(string connectionString)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DevelopmentSeed:PlayerId"] = DemoPlayerId,
                    ["DevelopmentSeed:PlayerName"] = "demo",
                }
            )
            .Build();
        return new ServiceCollection()
            .AddSingleton(configuration)
            .AddDbContext<LeaderboardDbContext>(options => options.UseNpgsql(connectionString))
            .BuildServiceProvider();
    }
}
