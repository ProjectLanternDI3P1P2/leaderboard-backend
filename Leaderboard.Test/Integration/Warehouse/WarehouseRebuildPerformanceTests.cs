using System.Diagnostics;
using FluentAssertions;
using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Test.Warehouse;

namespace Leaderboard.Test.Integration.Warehouse;

/// <summary>
/// US-DATA-03 criterion 5. Seeding millions of events takes minutes, so it only runs
/// when <c>LEADERBOARD_PERF_EVENT_COUNT</c> is set, for example to 5000000.
/// </summary>
public sealed class WarehouseRebuildPerformanceTests
{
    private const string EventCountVariable = "LEADERBOARD_PERF_EVENT_COUNT";
    private const int SeedChunkSize = 50_000;
    private static readonly TimeSpan BudgetFor5Million = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task AC5_FullRebuild_StaysWithinThirtyMinutesForFiveMillionEvents()
    {
        if (
            !int.TryParse(
                Environment.GetEnvironmentVariable(EventCountVariable),
                out int eventCount
            )
        )
        {
            Assert.Skip($"Set {EventCountVariable} to run the rebuild performance test.");
        }

        // Arrange: 100k players, 3 contract event types, 1 % redeliveries.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using WarehouseTestHarness harness = await WarehouseTestHarness.CreateAsync(
            cancellationToken
        );
        Random random = new(5);
        Stopwatch seeding = Stopwatch.StartNew();
        for (int seeded = 0; seeded < eventCount; seeded += SeedChunkSize)
        {
            List<RawEvent> chunk = [];
            for (int i = seeded; i < Math.Min(seeded + SeedChunkSize, eventCount); i++)
            {
                DateTimeOffset receivedAt = RawEventBuilder.Origin.AddMilliseconds(i / 4);
                chunk.Add(
                    i % 100 == 99 && chunk.Count > 0
                        ? RawEventBuilder.Redelivery(chunk[^1], receivedAt)
                        : RawEventBuilder.Action(
                            new Guid(random.Next(1, 100_001), 0, 0, new byte[8]).ToString(),
                            new[]
                            {
                                "combat.skill-used",
                                "combat.consumable-used",
                                "rewards.gold-earned",
                            }[random.Next(3)],
                            receivedAt,
                            value: (ulong)random.Next(1, 100)
                        )
                );
            }

            await harness.RawEventStore.AppendAsync(chunk, cancellationToken);
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Seeded {eventCount} events in {seeding.Elapsed}."
        );
        WarehouseRebuildReport started = await harness.StartAsync(cancellationToken);

        // Act
        Stopwatch rebuilding = Stopwatch.StartNew();
        await harness.RunAsync(new WarehouseRebuildOptions().BatchSize, cancellationToken);
        rebuilding.Stop();

        // Assert
        WarehouseRebuild rebuild = await harness.GetRebuildAsync(started.Id);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Rebuilt {rebuild.EventsProcessed} events in {rebuilding.Elapsed} ({rebuild.EventsProcessed / rebuilding.Elapsed.TotalSeconds:F0} events/s)."
        );
        rebuild.Status.Should().Be(WarehouseRebuildStatus.Completed);
        rebuild.EventsProcessed.Should().Be(eventCount);
        rebuilding.Elapsed.Should().BeLessThan(BudgetFor5Million * (eventCount / 5_000_000d));
    }
}
