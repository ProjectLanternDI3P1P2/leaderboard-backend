using FluentAssertions;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.Repositories;
using Leaderboard.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Leaderboard.Test.Integration.RawEvents;

public sealed class RawEventStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private MongoTestDatabase database = null!;
    private MongoRawEventStore store = null!;

    public async ValueTask InitializeAsync()
    {
        database = await MongoTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        store = new MongoRawEventStore(
            database.Client,
            Options.Create(
                new MongoOptions
                {
                    ConnectionString = MongoTestDatabase.ConnectionString,
                    DatabaseName = database.DatabaseName,
                    CollectionName = MongoTestDatabase.CollectionName,
                }
            )
        );
    }

    public async ValueTask DisposeAsync() => await database.DisposeAsync();

    private IMongoCollection<BsonDocument> Collection =>
        database.Database.GetCollection<BsonDocument>(MongoTestDatabase.CollectionName);

    private static RawEvent NewEvent(DateTimeOffset receivedAt, string? transactionId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            TransactionId = transactionId,
            Type = "combat.finished",
            Version = 1,
            CorrelationId = "correlation-1",
            OccurredAtUtc = receivedAt.AddSeconds(-1),
            ReceivedAt = receivedAt,
            Producer = "combat",
            Payload = [1, 2, 3],
        };

    private async Task<List<RawEvent>> ReadAsync(DateTimeOffset from, DateTimeOffset to)
    {
        List<RawEvent> events = [];
        await foreach (
            RawEvent rawEvent in store.GetByPeriodAsync(
                from,
                to,
                TestContext.Current.CancellationToken
            )
        )
        {
            events.Add(rawEvent);
        }

        return events;
    }

    [Fact]
    public void AC0_StoreContractExposesNoMutation()
    {
        string[] methods = typeof(IRawEventStore).GetMethods().Select(m => m.Name).ToArray();

        methods
            .Should()
            .BeEquivalentTo(
                nameof(IRawEventStore.AppendAsync),
                nameof(IRawEventStore.GetByPeriodAsync)
            );
    }

    [Fact]
    public async Task AC0_ApplicationRoleCannotMutateTheCollection()
    {
        if (!await database.IsAuthenticationEnabledAsync())
        {
            Assert.Skip("MongoDB runs without authentication, roles are not enforced.");
        }

        IMongoDatabase db = database.Database;
        await db.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                { "createRole", "eventAppender" },
                {
                    "privileges",
                    new BsonArray
                    {
                        new BsonDocument
                        {
                            {
                                "resource",
                                new BsonDocument
                                {
                                    { "db", database.DatabaseName },
                                    { "collection", MongoTestDatabase.CollectionName },
                                }
                            },
                            {
                                "actions",
                                new BsonArray { "insert", "find" }
                            },
                        },
                    }
                },
                { "roles", new BsonArray() },
            },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await db.RunCommandAsync<BsonDocument>(
            new BsonDocument
            {
                { "createUser", "appender" },
                { "pwd", "appender-password" },
                {
                    "roles",
                    new BsonArray { "eventAppender" }
                },
            },
            cancellationToken: TestContext.Current.CancellationToken
        );

        var appClient = new MongoClient(
            new MongoClientSettings
            {
                Server = new MongoServerAddress("localhost", 27017),
                DirectConnection = true,
                Credential = MongoCredential.CreateCredential(
                    database.DatabaseName,
                    "appender",
                    "appender-password"
                ),
            }
        );
        IMongoCollection<BsonDocument> collection = appClient
            .GetDatabase(database.DatabaseName)
            .GetCollection<BsonDocument>(MongoTestDatabase.CollectionName);

        Func<Task>[] mutations =
        [
            () =>
                collection.UpdateOneAsync(
                    FilterDefinition<BsonDocument>.Empty,
                    Builders<BsonDocument>.Update.Set("x", 1)
                ),
            () => collection.DeleteOneAsync(FilterDefinition<BsonDocument>.Empty),
            () => collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty),
            () =>
                appClient
                    .GetDatabase(database.DatabaseName)
                    .DropCollectionAsync(MongoTestDatabase.CollectionName),
            () =>
                collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(new BsonDocument("x", 1))
                ),
        ];

        foreach (Func<Task> mutation in mutations)
        {
            await mutation.Should().ThrowAsync<MongoException>();
        }
    }

    [Fact]
    public async Task AC1_EventsWithTheSameTransactionIdAreAllRetained()
    {
        await store.AppendAsync(
            [NewEvent(Now, "tx-1"), NewEvent(Now.AddSeconds(1), "tx-1")],
            TestContext.Current.CancellationToken
        );

        long count = await Collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("transactionId", "tx-1"),
            cancellationToken: TestContext.Current.CancellationToken
        );

        count.Should().Be(2);
    }

    [Fact]
    public async Task AC2_AnOldEventIsReadBackUnchangedAndNoTtlIndexExists()
    {
        RawEvent old = NewEvent(Now.AddYears(-1), "tx-old");
        await store.AppendAsync([old], TestContext.Current.CancellationToken);

        List<RawEvent> events = await ReadAsync(Now.AddYears(-1).AddDays(-1), Now);

        events.Should().ContainSingle().Which.Should().BeEquivalentTo(old);
        List<BsonDocument> indexes = await (
            await Collection.Indexes.ListAsync(TestContext.Current.CancellationToken)
        ).ToListAsync(TestContext.Current.CancellationToken);
        indexes.Should().NotContain(index => index.Contains("expireAfterSeconds"));
    }

    [Fact]
    public async Task AC3_PeriodReadFiltersAndSortsByReceiptWithExclusiveUpperBound()
    {
        RawEvent before = NewEvent(Now.AddMinutes(-1));
        RawEvent first = NewEvent(Now);
        RawEvent second = NewEvent(Now.AddMinutes(5));
        RawEvent onUpperBound = NewEvent(Now.AddMinutes(10));
        await store.AppendAsync(
            [second, onUpperBound, before, first],
            TestContext.Current.CancellationToken
        );

        List<RawEvent> events = await ReadAsync(Now, Now.AddMinutes(10));

        events.Select(e => e.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task AppendWithNoEventsIsANoOp()
    {
        await store.AppendAsync([], TestContext.Current.CancellationToken);

        (await ReadAsync(Now.AddYears(-5), Now.AddYears(5))).Should().BeEmpty();
    }
}
