using Leaderboard.Infrastructure.Persistence.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leaderboard.Test.Integration;

/// <summary>An ephemeral MongoDB database, dropped when the test finishes.</summary>
public sealed class MongoTestDatabase : IAsyncDisposable
{
    public const string CollectionName = "raw_events";
    private const string EnvironmentVariable = "LEADERBOARD_TEST_MONGO_CONNECTION";

    private MongoTestDatabase(MongoClient client, string databaseName)
    {
        Client = client;
        DatabaseName = databaseName;
    }

    public MongoClient Client { get; }
    public string DatabaseName { get; }
    public IMongoDatabase Database => Client.GetDatabase(DatabaseName);

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable)
        ?? "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";

    public static async Task<MongoTestDatabase> CreateAsync(CancellationToken cancellationToken)
    {
        var client = new MongoClient(ConnectionString);
        var database = new MongoTestDatabase(client, $"leaderboard_test_{Guid.NewGuid():N}");
        await RawEventCollectionInitializer.EnsureAsync(
            database.Database,
            CollectionName,
            cancellationToken
        );
        return database;
    }

    /// <summary>True when the server enforces authentication, so roles can be tested.</summary>
    public async Task<bool> IsAuthenticationEnabledAsync()
    {
        try
        {
            await Client.GetDatabase("admin").ListCollectionNamesAsync();
            BsonDocument status = await Client
                .GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("connectionStatus", 1));
            return status["authInfo"]["authenticatedUsers"].AsBsonArray.Count > 0;
        }
        catch (MongoCommandException)
        {
            return true;
        }
    }

    public async ValueTask DisposeAsync() => await Client.DropDatabaseAsync(DatabaseName);
}
