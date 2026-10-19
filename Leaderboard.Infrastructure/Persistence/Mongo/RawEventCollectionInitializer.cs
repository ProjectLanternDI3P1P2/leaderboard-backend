using MongoDB.Bson;
using MongoDB.Driver;

namespace Leaderboard.Infrastructure.Persistence.Mongo;

/// <summary>
/// Creates the raw events collection, its indexes and its schema validator.
/// Run it with the migration account: the application role cannot create indexes.
/// There is deliberately no TTL index, no capped collection and no unique index,
/// so nothing is ever purged or rejected as a duplicate.
/// </summary>
public static class RawEventCollectionInitializer
{
    public static async Task EnsureAsync(
        IMongoDatabase database,
        string collectionName,
        CancellationToken cancellationToken
    )
    {
        using IAsyncCursor<string> names = await database.ListCollectionNamesAsync(
            cancellationToken: cancellationToken
        );
        if (!(await names.ToListAsync(cancellationToken)).Contains(collectionName))
        {
            await database.CreateCollectionAsync(
                collectionName,
                new CreateCollectionOptions<BsonDocument>
                {
                    Validator = new BsonDocumentFilterDefinition<BsonDocument>(
                        new BsonDocument("$jsonSchema", Schema())
                    ),
                },
                cancellationToken
            );
        }

        IMongoCollection<BsonDocument> collection = database.GetCollection<BsonDocument>(
            collectionName
        );
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument { { "receivedAt", 1 }, { "_id", 1 } },
                    new CreateIndexOptions { Name = "receivedAt_id" }
                ),
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument("transactionId", 1),
                    new CreateIndexOptions { Name = "transactionId" }
                ),
                new CreateIndexModel<BsonDocument>(
                    new BsonDocument("messageId", 1),
                    new CreateIndexOptions { Name = "messageId" }
                ),
            ],
            cancellationToken
        );
    }

    private static BsonDocument Schema() =>
        new()
        {
            { "bsonType", "object" },
            {
                "required",
                new BsonArray
                {
                    "messageId",
                    "type",
                    "version",
                    "correlationId",
                    "occurredAt",
                    "receivedAt",
                    "producer",
                    "payload",
                }
            },
            {
                "properties",
                new BsonDocument
                {
                    { "messageId", new BsonDocument("bsonType", "binData") },
                    { "transactionId", new BsonDocument("bsonType", "string") },
                    { "type", new BsonDocument("bsonType", "string") },
                    { "version", new BsonDocument("bsonType", "int") },
                    { "correlationId", new BsonDocument("bsonType", "string") },
                    { "causationId", new BsonDocument("bsonType", "string") },
                    { "occurredAt", new BsonDocument("bsonType", "date") },
                    { "receivedAt", new BsonDocument("bsonType", "date") },
                    { "producer", new BsonDocument("bsonType", "string") },
                    { "payload", new BsonDocument("bsonType", "binData") },
                }
            },
        };
}
