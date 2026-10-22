using Leaderboard.Domain.Collection;

namespace Leaderboard.Domain.Repositories;

public interface IRawEventRepository
{
    // Completion means a durable acknowledged insert. Uncertain outcomes must throw.
    Task AddAsync(RawEventReceipt receipt, CancellationToken cancellationToken);

    Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken
    );
}
