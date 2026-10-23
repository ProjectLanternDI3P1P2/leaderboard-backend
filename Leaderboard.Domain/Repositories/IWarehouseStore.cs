using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Domain.Repositories;

/// <summary>
/// Write side of the warehouse used by a running rebuild. A rebuild runs outside the
/// command pipeline, so each operation persists its own changes.
/// </summary>
public interface IWarehouseStore
{
    /// <summary>
    /// Starts the oldest pending rebuild, or returns <c>null</c> when none is pending.
    /// Safe with several replicas: only one of them can start a given rebuild.
    /// </summary>
    Task<WarehouseRebuild?> StartNextPendingAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    );

    /// <summary>Opens the transaction in which one batch and its checkpoint are written together.</summary>
    Task<IWarehouseBatch> BeginBatchAsync(
        WarehouseRebuild rebuild,
        CancellationToken cancellationToken
    );

    /// <summary>Marks the rebuild complete and switches reads to its generation, atomically.</summary>
    Task ActivateAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken);

    /// <summary>
    /// Discards unsaved changes, such as the progress of a batch that failed to commit,
    /// so that a following status change does not persist them.
    /// </summary>
    Task ReloadAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken);

    /// <summary>Persists a status change made outside a batch (interruption, failure).</summary>
    Task SaveAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken);

    /// <summary>Deletes the rows of every generation that is neither active nor previous.</summary>
    Task DiscardUnusedGenerationsAsync(CancellationToken cancellationToken);

    /// <summary>Marks as interrupted the running rebuilds whose heartbeat is older than <paramref name="staleBefore"/>.</summary>
    Task<int> InterruptStaleAsync(
        DateTimeOffset staleBefore,
        DateTimeOffset now,
        CancellationToken cancellationToken
    );
}

/// <summary>One batch of a rebuild: either everything is committed, or nothing is.</summary>
public interface IWarehouseBatch : IAsyncDisposable
{
    /// <summary>
    /// Records the message IDs as counted in this generation.
    /// </summary>
    /// <returns>The IDs that were not counted yet; the others are duplicates.</returns>
    Task<IReadOnlySet<Guid>> RegisterMessagesAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken
    );

    Task ApplyAsync(WarehouseDelta delta, CancellationToken cancellationToken);

    /// <summary>Saves the rebuild checkpoint and commits the batch.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
