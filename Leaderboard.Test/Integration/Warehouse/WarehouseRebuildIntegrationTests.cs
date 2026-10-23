using FluentAssertions;
using Leaderboard.Application.Features.StatisticsUseCase.GetLeagueTable;
using Leaderboard.Application.Features.StatisticsUseCase.GetPlayerStatistics;
using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Test.Warehouse;

namespace Leaderboard.Test.Integration.Warehouse;

/// <summary>US-DATA-03 acceptance criteria 1 to 4, on real PostgreSQL and MongoDB.</summary>
public sealed class WarehouseRebuildIntegrationTests : IAsyncLifetime
{
    private const int BatchSize = 7;

    private WarehouseTestHarness harness = null!;

    public async ValueTask InitializeAsync() =>
        harness = await WarehouseTestHarness.CreateAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await harness.DisposeAsync();

    /// <summary>
    /// 3 players over several action types, with redeliveries (same message ID, new
    /// receipt), events sharing one receipt date, an unreadable payload and an event
    /// naming no player.
    /// </summary>
    private static List<RawEvent> Dataset()
    {
        Random random = new(7);
        string[] players = [RawEventBuilder.Ari, RawEventBuilder.Bo, RawEventBuilder.Cy];
        string[] types = ["combat.skill-used", "combat.consumable-used", "rewards.gold-earned"];
        List<RawEvent> events = [];

        for (int i = 0; i < 60; i++)
        {
            // Every third event shares the previous receipt date.
            DateTimeOffset receivedAt = RawEventBuilder.Origin.AddMilliseconds(i - i % 3);
            events.Add(
                RawEventBuilder.Action(
                    players[random.Next(players.Length)],
                    types[random.Next(types.Length)],
                    receivedAt,
                    value: (ulong)random.Next(1, 50)
                )
            );
        }

        events.Add(RawEventBuilder.Redelivery(events[4], RawEventBuilder.Origin.AddMinutes(1)));
        events.Add(RawEventBuilder.Redelivery(events[30], RawEventBuilder.Origin.AddMinutes(2)));
        events.Add(RawEventBuilder.Redelivery(events[30], RawEventBuilder.Origin.AddMinutes(3)));
        events.Add(RawEventBuilder.Unreadable(RawEventBuilder.Origin.AddMinutes(4)));
        events.Add(RawEventBuilder.WithoutPlayer(RawEventBuilder.Origin.AddMinutes(5)));
        return events;
    }

    /// <summary>The direct calculation of criterion 1: LINQ over the deduplicated events.</summary>
    private static WarehouseContent DirectCalculation(IEnumerable<RawEvent> events)
    {
        var actions = events
            .Where(e => e.ParseStatus == "Valid")
            .Select(e => (Event: e, Message: GameEventEnvelope.Parser.ParseFrom(e.Payload)))
            .OrderBy(x => x.Event.ReceivedAt)
            .DistinctBy(x => x.Event.MessageId)
            .Select(x => new
            {
                Player = (
                    x.Message.SkillUsed?.Actor
                    ?? x.Message.ConsumableUsed?.Actor
                    ?? x.Message.GoldEarned.Actor
                ).PlayerId,
                Type = x.Message.EventType,
                Value = x.Message.SkillUsed is { } skill ? (decimal)skill.ManaSpent
                : x.Message.ConsumableUsed is { } used ? used.Quantity
                : x.Message.GoldEarned.Amount,
                OccurredAt = x.Message.OccurredAt.ToDateTimeOffset(),
            })
            .ToList();

        return new WarehouseContent(
            [
                .. actions
                    .GroupBy(a => a.Player)
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => new PlayerRow(g.Key, g.LongCount(), g.Max(a => a.OccurredAt))),
            ],
            [
                .. actions
                    .GroupBy(a => (a.Player, a.Type))
                    .OrderBy(g => g.Key.Player, StringComparer.Ordinal)
                    .ThenBy(g => g.Key.Type, StringComparer.Ordinal)
                    .Select(g => new ActionRow(
                        g.Key.Player,
                        g.Key.Type,
                        g.LongCount(),
                        g.Sum(a => a.Value)
                    )),
            ]
        );
    }

    [Fact]
    public async Task AC1_RebuildOfAnEmptyWarehouse_EqualsTheDirectCalculationOnDeduplicatedEvents()
    {
        // Arrange
        List<RawEvent> events = Dataset();
        await harness.RawEventStore.AppendAsync(events, TestContext.Current.CancellationToken);
        (await harness.GetLeagueTableAsync()).Entries.Should().BeEmpty();

        // Act
        WarehouseRebuildReport started = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);

        // Assert
        WarehouseRebuild rebuild = await harness.GetRebuildAsync(started.Id);
        rebuild.Status.Should().Be(WarehouseRebuildStatus.Completed);
        WarehouseContent warehouse = await harness.GetGenerationAsync(rebuild.GenerationId);
        warehouse.Should().BeEquivalentTo(DirectCalculation(events), o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task AC2_CompletedRebuild_ReportsEventsProcessedAndTheMostRecentOne()
    {
        // Arrange
        List<RawEvent> events = Dataset();
        await harness.RawEventStore.AppendAsync(events, TestContext.Current.CancellationToken);
        WarehouseRebuildReport started = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );

        // Act
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);

        // Assert
        WarehouseRebuildReport report = WarehouseRebuildReport.From(
            await harness.GetRebuildAsync(started.Id)
        );
        report.EventsProcessed.Should().Be(events.Count);
        report.EventsApplied.Should().Be(60);
        report.DuplicatesIgnored.Should().Be(3);
        report.EventsIgnored.Should().Be(2);
        report.LatestEventReceivedAt.Should().Be(events.Max(e => e.ReceivedAt));
        report.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AC3_RebuildInProgress_ReadsServeTheLastConsolidatedVersion()
    {
        // Arrange: a consolidated warehouse, then more events and a second rebuild.
        List<RawEvent> first = Dataset();
        await harness.RawEventStore.AppendAsync(first, TestContext.Current.CancellationToken);
        await harness.StartAsync(TestContext.Current.CancellationToken);
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);
        GetLeagueTableResult consolidated = await harness.GetLeagueTableAsync();
        GetPlayerStatisticsResult consolidatedAri = await harness.GetPlayerStatisticsAsync(
            RawEventBuilder.Ari
        );

        await harness.RawEventStore.AppendAsync(
            [
                RawEventBuilder.Action(
                    RawEventBuilder.Ari,
                    "combat.skill-used",
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    1000
                ),
            ],
            TestContext.Current.CancellationToken
        );
        WarehouseRebuildReport second = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        using CancellationTokenSource stop = new();

        // Act: the second rebuild stops halfway, its generation half written.
        Func<Task> run = () =>
            harness.RunAsync(
                BatchSize,
                stop.Token,
                new InterruptingRawEventStore(harness.RawEventStore, 30, stop)
            );
        await run.Should().ThrowAsync<OperationCanceledException>();

        // Assert
        (await harness.GetRebuildAsync(second.Id))
            .EventsProcessed.Should()
            .BeGreaterThan(0);
        (await harness.GetLeagueTableAsync()).Should().BeEquivalentTo(consolidated);
        (await harness.GetPlayerStatisticsAsync(RawEventBuilder.Ari))
            .Should()
            .BeEquivalentTo(consolidatedAri);
    }

    [Fact]
    public async Task AC4_InterruptedThenResumedRebuild_EqualsARebuildInOneGo()
    {
        // Arrange
        List<RawEvent> events = Dataset();
        await harness.RawEventStore.AppendAsync(events, TestContext.Current.CancellationToken);

        WarehouseRebuildReport oneGo = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);

        WarehouseRebuildReport resumed = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        using CancellationTokenSource stop = new();

        // Act: stop in the middle of events sharing a receipt date, then resume.
        Func<Task> run = () =>
            harness.RunAsync(
                BatchSize,
                stop.Token,
                new InterruptingRawEventStore(harness.RawEventStore, 23, stop)
            );
        await run.Should().ThrowAsync<OperationCanceledException>();
        (await harness.GetRebuildAsync(resumed.Id))
            .Status.Should()
            .Be(WarehouseRebuildStatus.Interrupted);

        await harness.ResumeAsync(resumed.Id, TestContext.Current.CancellationToken);
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);

        // Assert
        WarehouseRebuild oneGoRebuild = await harness.GetRebuildAsync(oneGo.Id);
        WarehouseRebuild resumedRebuild = await harness.GetRebuildAsync(resumed.Id);
        resumedRebuild.Status.Should().Be(WarehouseRebuildStatus.Completed);
        resumedRebuild.AttemptCount.Should().Be(2);
        (await harness.GetGenerationAsync(resumedRebuild.GenerationId))
            .Should()
            .BeEquivalentTo(
                await harness.GetGenerationAsync(oneGoRebuild.GenerationId),
                o => o.WithStrictOrdering()
            );
        WarehouseRebuildReport
            .From(resumedRebuild)
            .Should()
            .BeEquivalentTo(
                WarehouseRebuildReport.From(oneGoRebuild),
                o =>
                    o.Excluding(r => r.Id)
                        .Excluding(r => r.RequestedAt)
                        .Excluding(r => r.CompletedAt)
                        .Excluding(r => r.ConsolidatedUntil)
                        .Excluding(r => r.AttemptCount)
            );
    }

    [Fact]
    public async Task Resume_CompletedRebuild_ThrowsConflict()
    {
        // Arrange
        WarehouseRebuildReport started = await harness.StartAsync(
            TestContext.Current.CancellationToken
        );
        await harness.RunAsync(BatchSize, TestContext.Current.CancellationToken);

        // Act
        Func<Task> resume = () =>
            harness.ResumeAsync(started.Id, TestContext.Current.CancellationToken);

        // Assert
        await resume.Should().ThrowAsync<Leaderboard.Domain.Exceptions.ConflictException>();
    }
}
