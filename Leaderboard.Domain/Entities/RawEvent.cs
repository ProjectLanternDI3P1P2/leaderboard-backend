namespace Leaderboard.Domain.Entities;

/// <summary>An event exactly as received, kept as an immutable fact.</summary>
public sealed record RawEvent
{
    public required Guid Id { get; init; }
    public Guid? MessageId { get; init; }
    public string? TransactionId { get; init; }
    public string? Type { get; init; }
    public int? Version { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public DateTimeOffset? OccurredAtUtc { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public string? Producer { get; init; }

    /// <summary>The complete original AMQP body, including its envelope.</summary>
    public required byte[] Payload { get; init; }
    public string ParseStatus { get; init; } = "Valid";
    public string[] ValidationErrors { get; init; } = [];
    public string? Exchange { get; init; }
    public string? RoutingKey { get; init; }
    public string? BrokerMessageId { get; init; }
    public bool Redelivered { get; init; }
}
