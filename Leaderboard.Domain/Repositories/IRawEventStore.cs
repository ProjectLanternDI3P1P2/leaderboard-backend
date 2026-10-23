using Leaderboard.Domain.Entities;

namespace Leaderboard.Domain.Repositories;

/// <summary>
/// Append-only store of received events. It deliberately exposes no way to
/// update or delete an event, so immutability holds by design.
/// </summary>
public interface IRawEventStore
{
    /// <summary>Durably stores the events. Duplicates are kept, never deduplicated.</summary>
    Task AppendAsync(IReadOnlyCollection<RawEvent> events, CancellationToken cancellationToken);

    /// <summary>
    /// Events with <c>from &lt;= ReceivedAt &lt; to</c>, ordered by receipt date.
    /// </summary>
    IAsyncEnumerable<RawEvent> GetByPeriodAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken
    );
}
