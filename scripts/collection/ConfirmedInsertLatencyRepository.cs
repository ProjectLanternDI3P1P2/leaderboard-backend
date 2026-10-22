using System.Collections.Concurrent;
using System.Diagnostics;
using Leaderboard.Domain.Collection;
using Leaderboard.Domain.Repositories;

internal sealed class ConfirmedInsertLatencyRepository(IRawEventRepository inner)
    : IRawEventRepository
{
    private readonly ConcurrentDictionary<string, long> confirmedInsertTicks = new();

    public IReadOnlyDictionary<string, long> ConfirmedInsertTicks => confirmedInsertTicks;

    public async Task AddAsync(RawEventReceipt receipt, CancellationToken cancellationToken)
    {
        await inner.AddAsync(receipt, cancellationToken);
        if (receipt.BrokerMessageId is not null)
            confirmedInsertTicks[receipt.BrokerMessageId] = Stopwatch.GetTimestamp();
    }

    public Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken
    ) => inner.ReadPeriodAsync(fromInclusive, toExclusive, cancellationToken);
}
