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
    private const string BsonTypeKey = "bsonType";
    private const string MessageIdField = "messageId";
    private const string StringType = "string";

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
                    new BsonDocument(MessageIdField, 1),
                    new CreateIndexOptions { Name = MessageIdField }
                ),
            ],
            cancellationToken
        );
    }

    private static BsonDocument Schema() =>
        new()
        {
            { BsonTypeKey, "object" },
            {
                "required",
                new BsonArray { "receivedAt", "payload", "parseStatus", "validationErrors" }
            },
            {
                "properties",
                new BsonDocument
                {
                    { MessageIdField, NullableType("binData") },
                    { "transactionId", new BsonDocument(BsonTypeKey, StringType) },
                    { "type", NullableType(StringType) },
                    { "version", NullableType("int") },
                    { "correlationId", NullableType(StringType) },
                    { "causationId", new BsonDocument(BsonTypeKey, StringType) },
                    { "occurredAt", NullableType("date") },
                    { "receivedAt", new BsonDocument(BsonTypeKey, "date") },
                    { "producer", NullableType(StringType) },
                    { "payload", new BsonDocument(BsonTypeKey, "binData") },
                    {
                        "parseStatus",
                        new BsonDocument(
                            "enum",
                            new BsonArray { "Valid", "InvalidOrUnknown", "Unreadable" }
                        )
                    },
                    {
                        "validationErrors",
                        new BsonDocument
                        {
                            { BsonTypeKey, "array" },
                            { "items", new BsonDocument(BsonTypeKey, StringType) },
                        }
                    },
                }
            },
        };

    private static BsonDocument NullableType(string type) =>
        new(BsonTypeKey, new BsonArray { type, "null" });
}
