namespace Leaderboard.Domain.Collection;

/// <summary>One delivery attempt, independently identified from producer and business IDs.</summary>
public sealed record RawEventReceipt(
    string ReceiptId,
    DateTimeOffset ReceivedAt,
    byte[] Body,
    string? TransactionId,
    string? ProducerSquad,
    string? EventType,
    string ParseStatus,
    string[] ValidationErrors,
    string Exchange,
    string RoutingKey,
    string? BrokerMessageId,
    string? CorrelationId,
    bool Redelivered
);
