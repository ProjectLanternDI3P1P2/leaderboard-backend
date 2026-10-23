using Leaderboard.Application.Ports;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Leaderboard.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leaderboard.Application.Features.WarehouseRebuildUseCase.RunWarehouseRebuild;

/// <summary>
/// Recalculates a warehouse generation from the raw event store. Events are read in
/// receipt order up to the rebuild's fixed bound, deduplicated on their message ID and
/// written batch by batch, each batch committed with its checkpoint. A resumed rebuild
/// restarts after its last checkpoint and ends with the same warehouse as an
/// uninterrupted one.
/// </summary>
public sealed class WarehouseRebuildRunner(
    IWarehouseStore store,
    IRawEventStore rawEventStore,
    IPlayerActionDecoder decoder,
    IClock clock,
    IOptions<WarehouseRebuildOptions> options,
    ILogger<WarehouseRebuildRunner> logger
)
{
    /// <returns><c>false</c> when no rebuild was pending.</returns>
    public async Task<bool> RunNextAsync(CancellationToken cancellationToken)
    {
        WarehouseRebuild? rebuild = await store.StartNextPendingAsync(
            clock.UtcNow,
            cancellationToken
        );
        if (rebuild is null)
        {
            return false;
        }

        using IDisposable? scope = logger.BeginScope(
            new Dictionary<string, object> { ["RebuildId"] = rebuild.Id }
        );
        logger.LogInformation(
            "Warehouse rebuild {RebuildId} started (attempt {AttemptCount}), events received before {ConsolidatedUntil}",
            rebuild.Id,
            rebuild.AttemptCount,
            rebuild.ConsolidatedUntil
        );

        try
        {
            await RebuildAsync(rebuild, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await store.ReloadAsync(rebuild, CancellationToken.None);
            rebuild.Interrupt(clock.UtcNow);
            await store.SaveAsync(rebuild, CancellationToken.None);
            logger.LogWarning(
                "Warehouse rebuild {RebuildId} interrupted after {EventsProcessed} events; it can be resumed",
                rebuild.Id,
                rebuild.EventsProcessed
            );
            throw;
        }
        catch (Exception exception)
        {
            await store.ReloadAsync(rebuild, CancellationToken.None);
            rebuild.Fail(exception.Message, clock.UtcNow);
            await store.SaveAsync(rebuild, CancellationToken.None);
            logger.LogError(
                exception,
                "Warehouse rebuild {RebuildId} failed after {EventsProcessed} events; it can be resumed",
                rebuild.Id,
                rebuild.EventsProcessed
            );
        }

        return true;
    }

    private async Task RebuildAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken)
    {
        int batchSize = options.Value.BatchSize;

        // Restart at the receipt date of the checkpoint, skipping the events already read
        // at that exact date: several events can share it.
        DateTimeOffset from = rebuild.LatestEventReceivedAt ?? DateTimeOffset.UnixEpoch;
        var cursor = new Cursor(from, [.. rebuild.CursorEventIds]);
        HashSet<Guid> alreadyRead = [.. rebuild.CursorEventIds];

        List<RawEvent> batch = new(batchSize);
        await foreach (
            RawEvent rawEvent in rawEventStore.GetByPeriodAsync(
                from,
                rebuild.ConsolidatedUntil,
                cancellationToken
            )
        )
        {
            if (rawEvent.ReceivedAt == from && alreadyRead.Contains(rawEvent.Id))
            {
                continue;
            }

            batch.Add(rawEvent);
            if (batch.Count == batchSize)
            {
                await ApplyBatchAsync(rebuild, batch, cursor, cancellationToken);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await ApplyBatchAsync(rebuild, batch, cursor, cancellationToken);
        }

        rebuild.Complete(clock.UtcNow);
        await store.ActivateAsync(rebuild, cancellationToken);
        logger.LogInformation(
            "Warehouse rebuild {RebuildId} completed: {EventsProcessed} events processed, {EventsApplied} applied, latest received at {LatestEventReceivedAt}",
            rebuild.Id,
            rebuild.EventsProcessed,
            rebuild.EventsApplied,
            rebuild.LatestEventReceivedAt
        );

        try
        {
            await store.DiscardUnusedGenerationsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The new generation is already active; leftovers are removed by the next rebuild.
            logger.LogWarning(exception, "Could not discard unused warehouse generations");
        }
    }

    private async Task ApplyBatchAsync(
        WarehouseRebuild rebuild,
        List<RawEvent> batch,
        Cursor cursor,
        CancellationToken cancellationToken
    )
    {
        List<PlayerAction> actions = [];
        foreach (RawEvent rawEvent in batch)
        {
            cursor.Advance(rawEvent);
            if (decoder.Decode(rawEvent) is { } action)
            {
                actions.Add(action);
            }
        }

        List<PlayerAction> distinctActions = [.. actions.DistinctBy(action => action.MessageId)];

        await using IWarehouseBatch transaction = await store.BeginBatchAsync(
            rebuild,
            cancellationToken
        );
        IReadOnlySet<Guid> newMessageIds = await transaction.RegisterMessagesAsync(
            [.. distinctActions.Select(action => action.MessageId)],
            cancellationToken
        );
        List<PlayerAction> newActions =
        [
            .. distinctActions.Where(action => newMessageIds.Contains(action.MessageId)),
        ];

        await transaction.ApplyAsync(
            PlayerActionAggregator.Aggregate(newActions),
            cancellationToken
        );
        rebuild.RecordBatch(
            new WarehouseBatchProgress(
                EventsProcessed: batch.Count,
                EventsApplied: newActions.Count,
                DuplicatesIgnored: actions.Count - newActions.Count,
                EventsIgnored: batch.Count - actions.Count,
                cursor.ReceivedAt,
                [.. cursor.EventIds]
            ),
            clock.UtcNow
        );
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed class Cursor(DateTimeOffset receivedAt, HashSet<Guid> eventIds)
    {
        public DateTimeOffset ReceivedAt { get; private set; } = receivedAt;
        public HashSet<Guid> EventIds { get; private set; } = eventIds;

        public void Advance(RawEvent rawEvent)
        {
            if (rawEvent.ReceivedAt != ReceivedAt)
            {
                ReceivedAt = rawEvent.ReceivedAt;
                EventIds = [];
            }

            EventIds.Add(rawEvent.Id);
        }
    }
}
