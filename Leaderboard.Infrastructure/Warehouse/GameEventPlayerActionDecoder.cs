using Google.Protobuf;
using Leaderboard.Application.Ports;
using Leaderboard.Contracts.Events.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Infrastructure.Warehouse;

/// <summary>Decodes the <see cref="GameEventMessage"/> payload every microservice publishes.</summary>
public sealed class GameEventPlayerActionDecoder : IPlayerActionDecoder
{
    public PlayerAction? Decode(RawEvent rawEvent)
    {
        GameEventMessage message;
        try
        {
            message = GameEventMessage.Parser.ParseFrom(rawEvent.Payload);
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(message.UserId))
        {
            return null;
        }

        if (!TryReadValue(message.Data, out decimal value))
        {
            return null;
        }

        return new PlayerAction(
            rawEvent.MessageId,
            message.UserId,
            string.IsNullOrWhiteSpace(message.ActionType) ? rawEvent.Type : message.ActionType,
            value,
            message.Timestamp?.ToDateTimeOffset() ?? rawEvent.OccurredAtUtc
        );
    }

    private static bool TryReadValue(EventData? data, out decimal value)
    {
        value = 0;
        if (data is not { HasValueNumeric: true })
        {
            return true;
        }

        // NaN, infinities and values beyond decimal range cannot be summed exactly.
        if (!double.IsFinite(data.ValueNumeric))
        {
            return false;
        }

        try
        {
            value = (decimal)data.ValueNumeric;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
