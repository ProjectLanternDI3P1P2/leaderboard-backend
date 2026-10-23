using Leaderboard.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Leaderboard.Infrastructure.Persistence.Mongo;

internal sealed class RawEventDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; init; }

    [BsonElement("messageId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid MessageId { get; init; }

    [BsonElement("transactionId")]
    [BsonIgnoreIfNull]
    public string? TransactionId { get; init; }

    [BsonElement("type")]
    public string Type { get; init; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; init; }

    [BsonElement("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [BsonElement("causationId")]
    [BsonIgnoreIfNull]
    public string? CausationId { get; init; }

    [BsonElement("occurredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime OccurredAt { get; init; }

    [BsonElement("receivedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ReceivedAt { get; init; }

    [BsonElement("producer")]
    public string Producer { get; init; } = string.Empty;

    [BsonElement("payload")]
    public byte[] Payload { get; init; } = [];

    public static RawEventDocument From(RawEvent rawEvent) =>
        new()
        {
            Id = rawEvent.Id,
            MessageId = rawEvent.MessageId,
            TransactionId = rawEvent.TransactionId,
            Type = rawEvent.Type,
            Version = rawEvent.Version,
            CorrelationId = rawEvent.CorrelationId,
            CausationId = rawEvent.CausationId,
            OccurredAt = rawEvent.OccurredAtUtc.UtcDateTime,
            ReceivedAt = rawEvent.ReceivedAt.UtcDateTime,
            Producer = rawEvent.Producer,
            Payload = rawEvent.Payload,
        };

    public RawEvent ToDomain() =>
        new()
        {
            Id = Id,
            MessageId = MessageId,
            TransactionId = TransactionId,
            Type = Type,
            Version = Version,
            CorrelationId = CorrelationId,
            CausationId = CausationId,
            OccurredAtUtc = new DateTimeOffset(OccurredAt, TimeSpan.Zero),
            ReceivedAt = new DateTimeOffset(ReceivedAt, TimeSpan.Zero),
            Producer = Producer,
            Payload = Payload,
        };
}
