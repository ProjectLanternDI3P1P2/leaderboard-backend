using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Leaderboard.Contracts.Events.V1;
using Leaderboard.Domain.Entities;

namespace Leaderboard.Test.Warehouse;

/// <summary>Raw events as the consumer stores them, with a <see cref="GameEventMessage"/> payload.</summary>
public static class RawEventBuilder
{
    public static readonly DateTimeOffset Origin = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    public static RawEvent Action(
        string playerId,
        string actionType,
        DateTimeOffset receivedAt,
        double? value = null,
        Guid? messageId = null
    )
    {
        GameEventMessage message = new()
        {
            Version = "1",
            UserId = playerId,
            ActionType = actionType,
            Timestamp = Timestamp.FromDateTimeOffset(receivedAt.AddSeconds(-1)),
            Data = value is { } numeric
                ? new EventData { ValueNumeric = numeric }
                : new EventData(),
        };

        return Raw(message.ToByteArray(), receivedAt, messageId);
    }

    /// <summary>A redelivery: same message, new receipt.</summary>
    public static RawEvent Redelivery(RawEvent original, DateTimeOffset receivedAt) =>
        original with
        {
            Id = Guid.NewGuid(),
            ReceivedAt = receivedAt,
        };

    public static RawEvent Unreadable(DateTimeOffset receivedAt) =>
        Raw([0xFF, 0xFF, 0xFF], receivedAt, messageId: null);

    public static RawEvent WithoutPlayer(DateTimeOffset receivedAt) =>
        Raw(new GameEventMessage { ActionType = "attack" }.ToByteArray(), receivedAt, null);

    private static RawEvent Raw(byte[] payload, DateTimeOffset receivedAt, Guid? messageId) =>
        new()
        {
            Id = Guid.NewGuid(),
            MessageId = messageId ?? Guid.NewGuid(),
            Type = "game.action",
            Version = 1,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAtUtc = receivedAt.AddSeconds(-1),
            ReceivedAt = receivedAt,
            Producer = "combat",
            Payload = payload,
        };
}
