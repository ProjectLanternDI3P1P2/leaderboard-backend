using Google.Protobuf;
using Leaderboard.Application.Ports;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Infrastructure.Warehouse;

/// <summary>Projects one contract event into one contribution per affected player.</summary>
public sealed class GameEventPlayerActionDecoder : IPlayerActionDecoder
{
    public IReadOnlyList<PlayerAction> Decode(RawEvent rawEvent)
    {
        if (rawEvent.ParseStatus != "Valid")
            return [];

        GameEventEnvelope message;
        try
        {
            message = GameEventEnvelope.Parser.ParseFrom(rawEvent.Payload);
        }
        catch (InvalidProtocolBufferException)
        {
            return [];
        }
        if (GameEventContract.Validate(message).Count != 0)
            return [];

        IEnumerable<Actor> actors = message.PayloadCase switch
        {
            GameEventEnvelope.PayloadOneofCase.DungeonRunStarted => message
                .DungeonRunStarted
                .Participants,
            GameEventEnvelope.PayloadOneofCase.FloorEntered => message.FloorEntered.Participants,
            GameEventEnvelope.PayloadOneofCase.DungeonRunEnded => message
                .DungeonRunEnded
                .Participants,
            GameEventEnvelope.PayloadOneofCase.CombatStarted => message.CombatStarted.Participants,
            GameEventEnvelope.PayloadOneofCase.CombatEnded => message.CombatEnded.Participants,
            GameEventEnvelope.PayloadOneofCase.CombatCompleted => message
                .CombatCompleted
                .Result
                .Participants,
            GameEventEnvelope.PayloadOneofCase.SkillUsed => [message.SkillUsed.Actor],
            GameEventEnvelope.PayloadOneofCase.ConsumableUsed => [message.ConsumableUsed.Actor],
            GameEventEnvelope.PayloadOneofCase.HeroCreated => [message.HeroCreated.Actor],
            GameEventEnvelope.PayloadOneofCase.ParticipantJoined =>
            [
                message.ParticipantJoined.Actor,
            ],
            GameEventEnvelope.PayloadOneofCase.ParticipantLeft => [message.ParticipantLeft.Actor],
            GameEventEnvelope.PayloadOneofCase.ItemGranted => [message.ItemGranted.Actor],
            GameEventEnvelope.PayloadOneofCase.ItemEquipped => [message.ItemEquipped.Actor],
            GameEventEnvelope.PayloadOneofCase.GoldEarned => [message.GoldEarned.Actor],
            GameEventEnvelope.PayloadOneofCase.XpAwarded => [message.XpAwarded.Actor],
            GameEventEnvelope.PayloadOneofCase.LevelUp => [message.LevelUp.Actor],
            _ => [],
        };
        decimal value = message.PayloadCase switch
        {
            GameEventEnvelope.PayloadOneofCase.SkillUsed => message.SkillUsed.ManaSpent,
            GameEventEnvelope.PayloadOneofCase.ConsumableUsed => message.ConsumableUsed.Quantity,
            GameEventEnvelope.PayloadOneofCase.ItemGranted => message.ItemGranted.Quantity,
            GameEventEnvelope.PayloadOneofCase.GoldEarned => message.GoldEarned.Amount,
            GameEventEnvelope.PayloadOneofCase.XpAwarded => message.XpAwarded.Amount,
            GameEventEnvelope.PayloadOneofCase.LevelUp => message.LevelUp.NewLevel
                - message.LevelUp.PreviousLevel,
            _ => 0m,
        };
        return actors
            .DistinctBy(actor => actor.PlayerId)
            .Select(actor => new PlayerAction(
                Guid.Parse(message.MessageId),
                actor.PlayerId,
                message.EventType,
                value,
                message.OccurredAt.ToDateTimeOffset()
            ))
            .ToArray();
    }
}
