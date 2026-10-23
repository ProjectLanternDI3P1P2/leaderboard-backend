namespace Leaderboard.Domain.Entities;

/// <summary>An event exactly as received, kept as an immutable fact.</summary>
public sealed record RawEvent
{
    public required Guid Id { get; init; }
    public required Guid MessageId { get; init; }
    public string? TransactionId { get; init; }
    public required string Type { get; init; }
    public required int Version { get; init; }
    public required string CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public required string Producer { get; init; }
    public required byte[] Payload { get; init; }
}
