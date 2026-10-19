namespace Leaderboard.Infrastructure.Messaging;

public sealed class RawEventConsumerOptions
{
    public const string SectionName = "RawEventConsumer";

    public bool Enabled { get; init; }
    public string QueueName { get; init; } = "leaderboard.raw-events";
    public string DeadLetterQueueName { get; init; } = "leaderboard.raw-events.dead";
    public string BindingKey { get; init; } = "#";
    public int BatchSize { get; init; } = 100;
    public TimeSpan BatchTimeout { get; init; } = TimeSpan.FromMilliseconds(500);
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);
}
