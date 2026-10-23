using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Enums;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Leaderboard.Infrastructure.Persistence.Warehouse;

public sealed class PostgresWarehouseStore(LeaderboardDbContext dbContext) : IWarehouseStore
{
    public async Task<WarehouseRebuild?> StartNextPendingAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        WarehouseRebuild? rebuild = await dbContext
            .WarehouseRebuilds.Where(r => r.Status == WarehouseRebuildStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (rebuild is null)
        {
            return null;
        }

        rebuild.Start(now);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return rebuild;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another replica started it first.
            dbContext.Entry(rebuild).State = EntityState.Detached;
            return null;
        }
    }

    public async Task<IWarehouseBatch> BeginBatchAsync(
        WarehouseRebuild rebuild,
        CancellationToken cancellationToken
    )
    {
        IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );
        return new PostgresWarehouseBatch(dbContext, transaction, rebuild.GenerationId);
    }

    public async Task ActivateAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        WarehouseState state = await dbContext.WarehouseState.SingleAsync(
            s => s.Id == WarehouseState.SingletonId,
            cancellationToken
        );
        state.PreviousGenerationId = state.ActiveGenerationId;
        state.ActiveGenerationId = rebuild.GenerationId;
        state.ActivatedAt = rebuild.CompletedAt;
        state.ConsolidatedUntil = rebuild.ConsolidatedUntil;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReloadAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken)
    {
        await dbContext.Entry(rebuild).ReloadAsync(cancellationToken);
    }

    public async Task SaveAsync(WarehouseRebuild rebuild, CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DiscardUnusedGenerationsAsync(CancellationToken cancellationToken)
    {
        // Keep the active and previous generations (reads), and those of every rebuild
        // that can still run or be resumed.
        const string keptGenerations = $"""
            SELECT active_generation_id FROM warehouse_state WHERE active_generation_id IS NOT NULL
            UNION SELECT previous_generation_id FROM warehouse_state WHERE previous_generation_id IS NOT NULL
            UNION SELECT generation_id FROM {WarehouseRebuildConfiguration.TableName} WHERE status <> 'Completed'
            """;
        const string resumableGenerations = $"""
            SELECT generation_id FROM {WarehouseRebuildConfiguration.TableName} WHERE status <> 'Completed'
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {PlayerStatisticsConfiguration.TableName} WHERE generation_id NOT IN ({keptGenerations})",
            cancellationToken
        );
        await dbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {PlayerActionStatisticsConfiguration.TableName} WHERE generation_id NOT IN ({keptGenerations})",
            cancellationToken
        );
        // The deduplication set is only needed while a rebuild can still be resumed.
        await dbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM {WarehouseProcessedMessageConfiguration.TableName} WHERE generation_id NOT IN ({resumableGenerations})",
            cancellationToken
        );
    }

    public async Task<int> InterruptStaleAsync(
        DateTimeOffset staleBefore,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        List<WarehouseRebuild> stale = await dbContext
            .WarehouseRebuilds.Where(r =>
                r.Status == WarehouseRebuildStatus.Running && r.UpdatedAt < staleBefore
            )
            .ToListAsync(cancellationToken);

        foreach (WarehouseRebuild rebuild in stale)
        {
            rebuild.Interrupt(now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return stale.Count;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Its worker refreshed the heartbeat meanwhile: it is not stale after all.
            dbContext.ChangeTracker.Clear();
            return 0;
        }
    }
}
