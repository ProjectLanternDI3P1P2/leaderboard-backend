using Leaderboard.Application.Collection;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.Services;
using Leaderboard.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Leaderboard.Infrastructure.Collection;

public static class CollectionServiceRegistration
{
    public static IServiceCollection AddEventCollection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<RabbitCollectionOptions>()
            .Configure(x => configuration.GetSection("RabbitMq").Bind(x))
            .Validate(
                x => x.IsValid(),
                "Invalid RabbitMq collection options or catalogue subscriptions."
            )
            .ValidateOnStart();
        services
            .AddOptions<MongoArchiveOptions>()
            .Configure(x => configuration.GetSection("MongoArchive").Bind(x))
            .Validate(
                x => x.IsValid(),
                "Invalid MongoArchive options; use Journaled or Majority write concern."
            )
            .ValidateOnStart();
        services.TryAddSingleton<IClock, SystemClock>();
        services.AddSingleton<MongoRawEventRepository>();
        services.AddSingleton<IRawEventRepository>(sp =>
            sp.GetRequiredService<MongoRawEventRepository>()
        );
        services.AddTransient<ArchiveEvent>();
        services.AddHostedService<RabbitEventCollector>();
        return services;
    }
}
