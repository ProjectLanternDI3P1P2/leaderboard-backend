using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Domain.Entities;
using Leaderboard.Infrastructure.Persistence;
using Leaderboard.Infrastructure.Persistence.Repositories;
using Leaderboard.Test.Integration.Warehouse;
using Leaderboard.Test.Warehouse;
using Microsoft.EntityFrameworkCore;
using HeroRankingEntity = Leaderboard.Domain.Entities.HeroRanking;

namespace Leaderboard.Test.Integration.HeroRanking;

public sealed class ConsolidatedHeroRankingTests : IAsyncLifetime
{
    private WarehouseTestHarness harness = null!;

    public async ValueTask InitializeAsync() =>
        harness = await WarehouseTestHarness.CreateAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    [Fact]
    public async Task Completed_generation_serves_fifty_real_heroes_per_page_with_stable_ties()
    {
        var events = new List<RawEvent>();
        for (int index = 1; index <= 60; index++)
        {
            string heroId = HeroId(index);
            DateTimeOffset receivedAt = RawEventBuilder.Origin.AddSeconds(index * 2);
            events.Add(RawEventBuilder.HeroCreated(RawEventBuilder.Ari, heroId, receivedAt));
            events.Add(
                RawEventBuilder.XpAwarded(
                    RawEventBuilder.Ari,
                    heroId,
                    1000,
                    receivedAt.AddSeconds(1)
                )
            );
        }
        events.Add(RawEventBuilder.Redelivery(events[1], RawEventBuilder.Origin.AddMinutes(10)));
        await harness.RawEventStore.AppendAsync(events, TestContext.Current.CancellationToken);

        WarehouseRebuildReport rebuild = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        await harness.RunAsync(7, TestContext.Current.CancellationToken);

        await using LeaderboardDbContext database = harness.CreateDbContext();
        var queries = new HeroRankingQueries(database);
        var first = await queries.GetPublicAsync(1, 50, TestContext.Current.CancellationToken);
        var second = await queries.GetPublicAsync(2, 50, TestContext.Current.CancellationToken);

        Assert.Equal(60, first.TotalCount);
        Assert.Equal(50, first.Entries.Count);
        Assert.Equal(10, second.Entries.Count);
        Assert.Equal(
            Enumerable.Range(1, 60).Select(HeroId),
            first.Entries.Concat(second.Entries).Select(hero => hero.HeroId)
        );
        Assert.Equal(
            Enumerable.Range(1, 60),
            first.Entries.Concat(second.Entries).Select(hero => hero.Rank)
        );
        Assert.Equal(
            rebuild.Id,
            (
                await database
                    .WarehouseRebuilds.AsNoTracking()
                    .SingleAsync(TestContext.Current.CancellationToken)
            ).Id
        );
        Assert.NotNull(first.ConsolidatedAt);
    }

    [Fact]
    public async Task Interrupted_rebuild_keeps_the_last_complete_hero_score_visible()
    {
        string heroId = HeroId(1);
        await harness.RawEventStore.AppendAsync(
            [
                RawEventBuilder.HeroCreated(RawEventBuilder.Ari, heroId, RawEventBuilder.Origin),
                RawEventBuilder.XpAwarded(
                    RawEventBuilder.Ari,
                    heroId,
                    1000,
                    RawEventBuilder.Origin.AddSeconds(1)
                ),
            ],
            TestContext.Current.CancellationToken
        );
        await harness.StartAsync(TestContext.Current.CancellationToken);
        await harness.RunAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(1000, (await ReadPublicAsync()).Entries.Single().TotalXp);

        await harness.RawEventStore.AppendAsync(
            [
                RawEventBuilder.XpAwarded(
                    RawEventBuilder.Ari,
                    heroId,
                    2000,
                    DateTimeOffset.UtcNow.AddSeconds(-1)
                ),
            ],
            TestContext.Current.CancellationToken
        );
        WarehouseRebuildReport second = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        using CancellationTokenSource stop = new();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            harness.RunAsync(
                1,
                stop.Token,
                new InterruptingRawEventStore(harness.RawEventStore, 2, stop)
            )
        );
        Assert.Equal(1000, (await ReadPublicAsync()).Entries.Single().TotalXp);

        await harness.ResumeAsync(second.Id, TestContext.Current.CancellationToken);
        await harness.RunAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(2000, (await ReadPublicAsync()).Entries.Single().TotalXp);
    }

    [Fact]
    public async Task Empty_completed_generation_does_not_show_development_seed_rows()
    {
        await using (LeaderboardDbContext database = harness.CreateDbContext())
        {
            database.HeroRankings.Add(
                new HeroRankingEntity
                {
                    HeroId = HeroId(1),
                    HeroName = "Demo hero",
                    HeroClass = "warrior",
                    PlayerId = RawEventBuilder.Ari,
                    AllTimeXp = 1000,
                    CreatedAt = RawEventBuilder.Origin,
                    SeededAt = RawEventBuilder.Origin,
                }
            );
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await harness.StartAsync(TestContext.Current.CancellationToken);
        await harness.RunAsync(7, TestContext.Current.CancellationToken);

        var ranking = await ReadPublicAsync();
        Assert.Empty(ranking.Entries);
        Assert.Equal(0, ranking.TotalCount);
        Assert.NotNull(ranking.ConsolidatedAt);
    }

    private async Task<Leaderboard.Application.Features.HeroRanking.PublicHeroRanking> ReadPublicAsync()
    {
        await using LeaderboardDbContext database = harness.CreateDbContext();
        return await new HeroRankingQueries(database).GetPublicAsync(
            1,
            50,
            TestContext.Current.CancellationToken
        );
    }

    private static string HeroId(int index) => $"00000000-0000-0000-0000-{index:000000000000}";
}
