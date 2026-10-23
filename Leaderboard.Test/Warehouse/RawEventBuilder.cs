using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Test.Collection;

namespace Leaderboard.Test.Warehouse;

public static class RawEventBuilder
{
    public static readonly DateTimeOffset Origin = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    public const string Ari = "11111111-1111-1111-1111-111111111111";
    public const string Bo = "22222222-2222-2222-2222-222222222222";
    public const string Cy = "33333333-3333-3333-3333-333333333333";
    private static readonly Dictionary<string, GameEventEnvelope> Templates = new()
    {
        ["combat.skill-used"] = EventFixtures.Load("skill-positive.json"),
        ["combat.consumable-used"] = EventFixtures.Load("consumable-used.json"),
        ["rewards.gold-earned"] = EventFixtures.Load("gold-player-wallet.json"),
    };

    public static RawEvent Action(
        string playerId,
        string actionType,
        DateTimeOffset receivedAt,
        ulong? value = null,
        Guid? messageId = null
    )
    {
        var message = Templates[actionType].Clone();
        message.MessageId = (messageId ?? Guid.NewGuid()).ToString();
        message.OccurredAt = Timestamp.FromDateTimeOffset(receivedAt.AddSeconds(-1));
        Actor actor = message.PayloadCase switch
        {
            GameEventEnvelope.PayloadOneofCase.SkillUsed => message.SkillUsed.Actor,
            GameEventEnvelope.PayloadOneofCase.ConsumableUsed => message.ConsumableUsed.Actor,
            _ => message.GoldEarned.Actor,
        };
        actor.PlayerId = playerId;
        if (message.GoldEarned is { } gold)
            gold.Amount = value ?? 1;
        return FromEnvelope(message, receivedAt);
    }

    public static RawEvent FromEnvelope(GameEventEnvelope message, DateTimeOffset receivedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.TryParse(message.MessageId, out var id) ? id : null,
            Type = message.EventType,
            Version = (int)message.EventVersion,
            TransactionId = message.TransactionId,
            CorrelationId = message.HasCorrelationId ? message.CorrelationId : null,
            OccurredAtUtc = message.OccurredAt?.ToDateTimeOffset(),
            ReceivedAt = receivedAt,
            Producer = message.ProducerSquad.ToString(),
            Payload = message.ToByteArray(),
        };

    public static RawEvent Redelivery(RawEvent original, DateTimeOffset receivedAt) =>
        original with
        {
            Id = Guid.NewGuid(),
            ReceivedAt = receivedAt,
        };

    public static RawEvent Unreadable(DateTimeOffset receivedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            ReceivedAt = receivedAt,
            Payload = [255],
            ParseStatus = "Unreadable",
            ValidationErrors = ["Unreadable test message."],
        };

    public static RawEvent WithoutPlayer(DateTimeOffset receivedAt)
    {
        var message = EventFixtures.Load("hero-created.json");
        message.HeroCreated.Actor.PlayerId = "";
        return FromEnvelope(message, receivedAt) with { ParseStatus = "InvalidOrUnknown" };
    }
}
