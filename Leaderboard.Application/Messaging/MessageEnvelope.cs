namespace Leaderboard.Application.Messaging;

/// <summary>Broker-independent representation of an integration message.</summary>
public sealed record MessageEnvelope(
    Guid MessageId,
    string CorrelationId,
    string? CausationId,
    string Type,
    int Version,
    DateTimeOffset OccurredAtUtc,
    string Producer,
    ReadOnlyMemory<byte> Payload);
