using Leaderboard.Contracts.Collection.V1;
using RabbitMQ.Client;

namespace Leaderboard.Infrastructure.Collection;

public sealed class MongoArchiveOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string Database { get; set; } = "leaderboard_datalake";
    public string Collection { get; set; } = "raw_events";

    // Standalone: acknowledged primary + journal. Replica sets may explicitly select Majority.
    public string WriteConcern { get; set; } = "Journaled";

    public bool IsValid() =>
        Uri.TryCreate(ConnectionString, UriKind.Absolute, out var uri)
        && uri.Scheme is "mongodb" or "mongodb+srv"
        && !string.IsNullOrWhiteSpace(Database)
        && !Database.Contains('/')
        && !string.IsNullOrWhiteSpace(Collection)
        && WriteConcern is "Journaled" or "Majority";
}

public sealed class RabbitCollectionOptions
{
    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "leaderboard.collection.v1";
    public string DeadLetterExchangeName { get; set; } = "leaderboard.collection.dead-letter";
    public string DeadLetterQueueName { get; set; } = "leaderboard.collection.v1.dead";
    public ushort PrefetchCount { get; set; } = 64;
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaxDeliveryAttempts { get; set; } = 10;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public List<CollectionSubscription> Subscriptions { get; set; } =
        GameEventContract
            .Events.Select(x => new CollectionSubscription
            {
                ExchangeName = x.EventType.Split('.')[0] + ".events",
                RoutingKey = x.RoutingKey,
            })
            .ToList();

    public bool IsValid() =>
        !Enabled
        || (
            !string.IsNullOrWhiteSpace(HostName)
            && Port is > 0 and <= 65535
            && !string.IsNullOrWhiteSpace(UserName)
            && !string.IsNullOrWhiteSpace(VirtualHost)
            && !string.IsNullOrWhiteSpace(QueueName)
            && !string.IsNullOrWhiteSpace(DeadLetterExchangeName)
            && !string.IsNullOrWhiteSpace(DeadLetterQueueName)
            && QueueName != DeadLetterQueueName
            && PrefetchCount is > 0 and <= 1000
            && RetryDelaySeconds is >= 1 and <= 300
            && ReconnectDelaySeconds is >= 1 and <= 300
            && MaxDeliveryAttempts is >= 1 and <= 100
            && Subscriptions.Count == GameEventContract.Events.Count
            && Subscriptions.All(x => x.ExchangeName == x.RoutingKey.Split('.')[0] + ".events")
            && Subscriptions
                .Select(x => x.RoutingKey)
                .Order()
                .SequenceEqual(GameEventContract.Events.Select(x => x.RoutingKey).Order())
        );

    public ConnectionFactory CreateConnectionFactory() =>
        new()
        {
            HostName = HostName,
            Port = Port,
            VirtualHost = VirtualHost,
            UserName = UserName,
            Password = Password,
            // The hosted service reconnects and redeclares topology, including initial connection failures.
            AutomaticRecoveryEnabled = false,
            ConsumerDispatchConcurrency = 1,
        };
}

public sealed class CollectionSubscription
{
    public string ExchangeName { get; set; } = "";
    public string RoutingKey { get; set; } = "";
}
