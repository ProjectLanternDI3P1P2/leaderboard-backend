using Leaderboard.Application.Features.WarehouseRebuildUseCase;
using Leaderboard.Application.Ports;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Persistence.Warehouse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Leaderboard.Infrastructure.Warehouse;

public static class WarehouseServiceRegistration
{
    public static IServiceCollection AddWarehouse(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        WarehouseRebuildOptions options =
            configuration
                .GetSection(WarehouseRebuildOptions.SectionName)
                .Get<WarehouseRebuildOptions>()
            ?? new WarehouseRebuildOptions();
        Validate(options);

        services
            .AddSingleton(Options.Create(options))
            .AddSingleton<IPlayerActionDecoder, GameEventPlayerActionDecoder>()
            .AddScoped<IWarehouseStore, PostgresWarehouseStore>();

        if (options.IsWorkerEnabled)
        {
            services.AddHostedService<WarehouseRebuildWorker>();
        }

        return services;
    }

    private static void Validate(WarehouseRebuildOptions options)
    {
        if (
            options.BatchSize < 1
            || options.PollingInterval <= TimeSpan.Zero
            || options.HeartbeatTimeout <= TimeSpan.Zero
        )
        {
            throw new InvalidOperationException(
                "Warehouse:Rebuild: BatchSize, PollingInterval and HeartbeatTimeout must be positive."
            );
        }
    }
}
