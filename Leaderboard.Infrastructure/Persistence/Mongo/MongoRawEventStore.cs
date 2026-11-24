using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Leaderboard.Infrastructure.Persistence.Mongo;

/// <summary>
/// Append-only MongoDB store. Writes wait for a majority of the replica set and
/// the journal, so an acknowledged batch survives a node failure.
/// </summary>
public sealed class MongoRawEventStore : IRawEventStore
{
    private const int ReadBatchSize = 1000;

    private readonly IMongoCollection<RawEventDocument> _collection;

    public MongoRawEventStore(IMongoClient client, IOptions<MongoOptions> options)
    {
        MongoOptions settings = options.Value;
        _collection = client
            .GetDatabase(settings.DatabaseName)
            .GetCollection<RawEventDocument>(settings.CollectionName)
            .WithWriteConcern(WriteConcern.WMajority.With(journal: true));
    }

    public async Task AppendAsync(
        IReadOnlyCollection<RawEvent> events,
        CancellationToken cancellationToken
    )
    {
        if (events.Count == 0)
        {
            return;
        }

        await _collection.InsertManyAsync(
            events.Select(RawEventDocument.From),
            cancellationToken: cancellationToken
        );
    }

    public async IAsyncEnumerable<RawEvent> GetByPeriodAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        FilterDefinition<RawEventDocument> filter = Builders<RawEventDocument>.Filter.And(
            Builders<RawEventDocument>.Filter.Gte(e => e.ReceivedAt, from.UtcDateTime),
            Builders<RawEventDocument>.Filter.Lt(e => e.ReceivedAt, to.UtcDateTime)
        );

        using IAsyncCursor<RawEventDocument> cursor = await _collection.FindAsync(
            filter,
            new FindOptions<RawEventDocument>
            {
                Sort = Builders<RawEventDocument>
                    .Sort.Ascending(e => e.ReceivedAt)
                    .Ascending(e => e.Id),
                BatchSize = ReadBatchSize,
            },
            cancellationToken
        );

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (RawEventDocument document in cursor.Current)
            {
                yield return document.ToDomain();
            }
        }
    }
}
