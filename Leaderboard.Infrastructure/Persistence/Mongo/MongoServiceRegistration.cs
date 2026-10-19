using Leaderboard.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Leaderboard.Infrastructure.Persistence.Mongo;

public static class MongoServiceRegistration
{
    public static IServiceCollection AddMongoEventStore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        MongoOptions options =
            configuration.GetSection(MongoOptions.SectionName).Get<MongoOptions>()
            ?? new MongoOptions();
        // Like the PostgreSQL one, the connection string lives under ConnectionStrings.
        string? connectionString = configuration.GetConnectionString("Mongo");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            options = options with { ConnectionString = connectionString };
        }

        Validate(options);

        return services
            .AddSingleton(Options.Create(options))
            .AddSingleton<IMongoClient>(_ => new MongoClient(options.ConnectionString))
            .AddSingleton<IRawEventStore, MongoRawEventStore>();
    }

    private static void Validate(MongoOptions options)
    {
        if (
            string.IsNullOrWhiteSpace(options.ConnectionString)
            || string.IsNullOrWhiteSpace(options.DatabaseName)
            || string.IsNullOrWhiteSpace(options.CollectionName)
        )
        {
            throw new InvalidOperationException(
                "Mongo:ConnectionString, Mongo:DatabaseName and Mongo:CollectionName are required."
            );
        }
    }
}
