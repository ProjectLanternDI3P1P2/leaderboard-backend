using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Application.Features.WarehouseRebuildUseCase.RunWarehouseRebuild;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Warehouse;

/// <summary>
/// Runs pending warehouse rebuilds in the background, one at a time. A rebuild left
/// running by a crashed instance is marked interrupted, so the operator can resume it.
/// </summary>
public sealed class WarehouseRebuildWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WarehouseRebuildOptions> options,
    IClock clock,
    ILogger<WarehouseRebuildWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        WarehouseRebuildOptions settings = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            bool hasRun;
            try
            {
                hasRun = await RunOnceAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Warehouse rebuild worker iteration failed");
                hasRun = false;
            }

            if (!hasRun)
            {
                try
                {
                    await Task.Delay(settings.PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task<bool> RunOnceAsync(
        WarehouseRebuildOptions settings,
        CancellationToken stoppingToken
    )
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        DateTimeOffset now = clock.UtcNow;
        int interrupted = await scope
            .ServiceProvider.GetRequiredService<IWarehouseStore>()
            .InterruptStaleAsync(now - settings.HeartbeatTimeout, now, stoppingToken);
        if (interrupted > 0)
        {
            logger.LogWarning(
                "{Count} warehouse rebuild(s) lost their worker and were marked interrupted",
                interrupted
            );
        }

        return await scope
            .ServiceProvider.GetRequiredService<WarehouseRebuildRunner>()
            .RunNextAsync(stoppingToken);
    }
}
