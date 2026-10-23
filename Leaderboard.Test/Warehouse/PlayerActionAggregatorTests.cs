using FluentAssertions;
using Leaderboard.Domain.Services;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Test.Warehouse;

public class PlayerActionAggregatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static PlayerAction Action(string playerId, string type, decimal value, int minute) =>
        new(Guid.NewGuid(), playerId, type, value, Now.AddMinutes(minute));

    [Fact]
    public void Aggregate_Actions_SumsPerPlayerAndPerActionType()
    {
        // Arrange
        PlayerAction[] actions =
        [
            Action("ari", "attack", 10m, 1),
            Action("ari", "attack", 5.5m, 3),
            Action("ari", "potion", 0m, 2),
            Action("bo", "attack", 7m, 4),
        ];

        // Act
        WarehouseDelta delta = PlayerActionAggregator.Aggregate(actions);

        // Assert
        delta
            .Players.Should()
            .BeEquivalentTo([
                new PlayerStatisticsDelta("ari", 3, Now.AddMinutes(3)),
                new PlayerStatisticsDelta("bo", 1, Now.AddMinutes(4)),
            ]);
        delta
            .Actions.Should()
            .BeEquivalentTo([
                new PlayerActionStatisticsDelta("ari", "attack", 2, 15.5m),
                new PlayerActionStatisticsDelta("ari", "potion", 1, 0m),
                new PlayerActionStatisticsDelta("bo", "attack", 1, 7m),
            ]);
    }

    [Fact]
    public void Aggregate_SplitIntoBatches_AddsUpToTheSameTotals()
    {
        // Arrange
        Random random = new(42);
        List<PlayerAction> actions =
        [
            .. Enumerable
                .Range(0, 500)
                .Select(i =>
                    Action(
                        $"player-{random.Next(7)}",
                        $"type-{random.Next(3)}",
                        (decimal)Math.Round(random.NextDouble() * 100, 3),
                        i
                    )
                ),
        ];

        // Act
        WarehouseDelta whole = PlayerActionAggregator.Aggregate(actions);
        List<PlayerStatisticsDelta> summed =
        [
            .. actions
                .Chunk(37)
                .SelectMany(batch => PlayerActionAggregator.Aggregate(batch).Players)
                .GroupBy(delta => delta.PlayerId)
                .Select(group => new PlayerStatisticsDelta(
                    group.Key,
                    group.Sum(d => d.TotalActions),
                    group.Max(d => d.LastActionAt)
                )),
        ];

        // Assert
        summed.Should().BeEquivalentTo(whole.Players);
    }

    [Fact]
    public void Aggregate_NoAction_ReturnsAnEmptyDelta()
    {
        // Act
        WarehouseDelta delta = PlayerActionAggregator.Aggregate([]);

        // Assert
        delta.Players.Should().BeEmpty();
        delta.Actions.Should().BeEmpty();
    }
}
