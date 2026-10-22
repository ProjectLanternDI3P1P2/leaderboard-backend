namespace Leaderboard.Infrastructure.Messaging;

public sealed class RawEventConsumerOptions
{
    public const string SectionName = "RawEventConsumer";

    public bool Enabled { get; init; }
    public string QueueName { get; init; } = "leaderboard.raw-events";
    public string DeadLetterQueueName { get; init; } = "leaderboard.raw-events.dead";
    public string BindingKey { get; init; } = "#";

    /// <summary>
    /// Exchanges the consumer declares and binds its queue to with <see cref="BindingKey"/>.
    /// Empty by default: on the platform the broker routes every producer exchange to
    /// <c>RabbitMq:ExchangeName</c> (exchange-to-exchange bindings of the
    /// <c>rabbitmq-provisioning</c> job), and the service account may not configure
    /// producer exchanges. Set it only where no such routing exists, as in local compose.
    /// </summary>
    public List<string> SourceExchanges { get; init; } = [];
    public int BatchSize { get; init; } = 100;
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}
