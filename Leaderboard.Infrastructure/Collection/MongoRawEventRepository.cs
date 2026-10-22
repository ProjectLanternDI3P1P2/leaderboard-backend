using Leaderboard.Domain.Collection;
using Leaderboard.Domain.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leaderboard.Infrastructure.Collection;

public sealed class MongoRawEventRepository : IRawEventRepository
{
    private readonly IMongoCollection<BsonDocument> collection;

    public MongoRawEventRepository(IOptions<MongoArchiveOptions> options)
    {
        var value = options.Value;
        var settings = MongoClientSettings.FromConnectionString(value.ConnectionString);
        if (value.WriteConcern == "Majority" && string.IsNullOrEmpty(settings.ReplicaSetName))
            throw new InvalidOperationException(
                "Majority archival requires an explicit replicaSet connection string. Use Journaled on standalone MongoDB."
            );
        settings.WriteConcern =
            value.WriteConcern == "Majority"
                ? new WriteConcern("majority", journal: true, wTimeout: TimeSpan.FromSeconds(15))
                : new WriteConcern(1, journal: true, wTimeout: TimeSpan.FromSeconds(15));
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
        collection = new MongoClient(settings)
            .GetDatabase(value.Database)
            .GetCollection<BsonDocument>(value.Collection);
    }

    public Task InitializeAsync(CancellationToken cancellationToken) =>
        collection.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("ReceivedAt").Ascending("_id")
            ),
            cancellationToken: cancellationToken
        );

    public Task AddAsync(RawEventReceipt r, CancellationToken cancellationToken) =>
        collection.InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = r.ReceiptId,
                ["ReceivedAt"] = new BsonDateTime(r.ReceivedAt.UtcDateTime),
                ["Body"] = new BsonBinaryData(r.Body),
                ["TransactionId"] = Value(r.TransactionId),
                ["ProducerSquad"] = Value(r.ProducerSquad),
                ["EventType"] = Value(r.EventType),
                ["ParseStatus"] = r.ParseStatus,
                ["ValidationErrors"] = new BsonArray(r.ValidationErrors),
                ["Exchange"] = r.Exchange,
                ["RoutingKey"] = r.RoutingKey,
                ["BrokerMessageId"] = Value(r.BrokerMessageId),
                ["CorrelationId"] = Value(r.CorrelationId),
                ["Redelivered"] = r.Redelivered,
            },
            cancellationToken: cancellationToken
        );

    public async Task<IReadOnlyList<RawEventReceipt>> ReadPeriodAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken
    )
    {
        var filter =
            Builders<BsonDocument>.Filter.Gte("ReceivedAt", fromInclusive.UtcDateTime)
            & Builders<BsonDocument>.Filter.Lt("ReceivedAt", toExclusive.UtcDateTime);
        var documents = await collection
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("ReceivedAt").Ascending("_id"))
            .ToListAsync(cancellationToken);
        return documents.Select(FromDocument).ToArray();
    }

    private static BsonValue Value(string? value) =>
        value is null ? BsonNull.Value : new BsonString(value);

    private static string? Optional(BsonDocument d, string key) =>
        d[key].IsBsonNull ? null : d[key].AsString;

    public static RawEventReceipt FromDocument(BsonDocument d) =>
        new(
            d["_id"].AsString,
            new DateTimeOffset(d["ReceivedAt"].ToUniversalTime()),
            d["Body"].AsByteArray,
            Optional(d, "TransactionId"),
            Optional(d, "ProducerSquad"),
            Optional(d, "EventType"),
            d["ParseStatus"].AsString,
            d["ValidationErrors"].AsBsonArray.Select(x => x.AsString).ToArray(),
            d["Exchange"].AsString,
            d["RoutingKey"].AsString,
            Optional(d, "BrokerMessageId"),
            Optional(d, "CorrelationId"),
            d["Redelivered"].AsBoolean
        );
}
