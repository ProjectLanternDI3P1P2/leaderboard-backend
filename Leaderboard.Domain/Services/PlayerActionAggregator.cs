using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Domain.Services;

/// <summary>
/// The warehouse calculation rules. Every statistic is a sum or a maximum, so adding
/// the deltas of consecutive batches gives the same result as one calculation over
/// all the actions, whatever the batch boundaries.
/// </summary>
public static class PlayerActionAggregator
{
    public static WarehouseDelta Aggregate(IEnumerable<PlayerAction> actions)
    {
        List<PlayerAction> all = [.. actions];

        List<PlayerStatisticsDelta> players =
        [
            .. all.GroupBy(action => action.PlayerId, StringComparer.Ordinal)
                .Select(group => new PlayerStatisticsDelta(
                    group.Key,
                    group.LongCount(),
                    group.Sum(action => action.Value),
                    group.Max(action => action.OccurredAt)
                )),
        ];

        List<PlayerActionStatisticsDelta> perAction =
        [
            .. all.GroupBy(action => (action.PlayerId, action.ActionType))
                .Select(group => new PlayerActionStatisticsDelta(
                    group.Key.PlayerId,
                    group.Key.ActionType,
                    group.LongCount(),
                    group.Sum(action => action.Value)
                )),
        ];

        return new WarehouseDelta(players, perAction);
    }
}
