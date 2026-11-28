using Google.Protobuf;
using Leaderboard.Application.Ports;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Domain.Entities;
using Leaderboard.Domain.ValueObjects;

namespace Leaderboard.Infrastructure.Warehouse;

/// <summary>Extracts hero identity and persisted progression from validated facts.</summary>
public sealed class GameEventHeroRankingDecoder : IHeroRankingEventDecoder
{
    public IReadOnlyList<HeroRankingChange> Decode(RawEvent rawEvent)
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

        Actor actor;
        string? heroClassId = null;
        long? totalXpAfter = null;
        int? level = null;
        switch (message.PayloadCase)
        {
            case GameEventEnvelope.PayloadOneofCase.HeroCreated:
                actor = message.HeroCreated.Actor;
                heroClassId = message.HeroCreated.HeroClassId;
                level = 1;
                break;
            case GameEventEnvelope.PayloadOneofCase.XpAwarded:
                actor = message.XpAwarded.Actor;
                if (message.XpAwarded.TotalXpAfter > long.MaxValue)
                    return [];
                totalXpAfter = (long)message.XpAwarded.TotalXpAfter;
                break;
            case GameEventEnvelope.PayloadOneofCase.LevelUp:
                actor = message.LevelUp.Actor;
                if (message.LevelUp.NewLevel > int.MaxValue)
                    return [];
                level = (int)message.LevelUp.NewLevel;
                break;
            default:
                return [];
        }

        return
        [
            new HeroRankingChange(
                actor.HeroId,
                actor.PlayerId,
                heroClassId,
                totalXpAfter,
                level,
                message.OccurredAt.ToDateTimeOffset()
            ),
        ];
    }
}
