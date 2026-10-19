namespace Leaderboard.Infrastructure.Persistence.Mongo;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public string ConnectionString { get; init; } =
        "mongodb://localhost:27017/?replicaSet=rs0&directConnection=true";
    public string DatabaseName { get; init; } = "progression_lake";
    public string CollectionName { get; init; } = "raw_events";
}
