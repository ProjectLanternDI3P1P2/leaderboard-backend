using FluentAssertions;
using Leaderboard.Contracts.Collection.V1;
using Leaderboard.Infrastructure.Warehouse;
using Leaderboard.Test.Collection;

namespace Leaderboard.Test.Warehouse;

public class GameEventPlayerActionDecoderTests
{
    private readonly GameEventPlayerActionDecoder decoder = new();

    [Theory]
    [InlineData("skill-positive.json", 5)]
    [InlineData("skill-zero.json", 0)]
    [InlineData("consumable-used.json", 1)]
    [InlineData("item-stackable.json", 3)]
    [InlineData("gold-player-wallet.json", 40)]
    [InlineData("xp-awarded.json", 100)]
    [InlineData("level-up.json", 2)]
    public void Decode_NumericFacts_UsesTheQuantityInTheEventsOwnUnit(string file, int expected)
    {
        var message = EventFixtures.Load(file);
        decoder
            .Decode(RawEventBuilder.FromEnvelope(message, RawEventBuilder.Origin))
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be(expected);
    }

    [Fact]
    public void Decode_AllSixteenContractEvents_ContributesOncePerPlayer()
    {
        var messages = EventFixtures.Catalogue().ToArray();
        messages.Should().HaveCount(16);
        foreach (var message in messages)
        {
            var actions = decoder.Decode(
                RawEventBuilder.FromEnvelope(message, RawEventBuilder.Origin)
            );
            actions.Should().NotBeEmpty();
            actions.Select(a => a.PlayerId).Should().OnlyHaveUniqueItems();
            actions
                .Should()
                .OnlyContain(a =>
                    a.MessageId == Guid.Parse(message.MessageId)
                    && a.ActionType == message.EventType
                    && a.OccurredAt == message.OccurredAt.ToDateTimeOffset()
                );
        }
    }

    [Fact]
    public void Decode_MaximumGoldAmount_PreservesTheExactIntegerValue()
    {
        var message = EventFixtures.Load("gold-player-wallet.json");
        message.GoldEarned.Amount = ulong.MaxValue;
        decoder
            .Decode(RawEventBuilder.FromEnvelope(message, RawEventBuilder.Origin))
            .Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be((decimal)ulong.MaxValue);
    }

    [Fact]
    public void Decode_GroupEvent_ContributesOncePerPlayerEvenWithSeveralHeroes()
    {
        var message = EventFixtures.Load("run-started.json");
        var participants = message.DungeonRunStarted.Participants;
        participants.Add(
            new Actor { PlayerId = participants[0].PlayerId, HeroId = Guid.NewGuid().ToString() }
        );
        GameEventContract.Validate(message).Should().BeEmpty();
        var actions = decoder.Decode(RawEventBuilder.FromEnvelope(message, RawEventBuilder.Origin));
        actions
            .Select(a => a.PlayerId)
            .Should()
            .BeEquivalentTo(participants.Take(2).Select(a => a.PlayerId));
    }

    [Fact]
    public void Decode_UnreadableInvalidAndUnknownEvents_ContributeNothing()
    {
        decoder.Decode(RawEventBuilder.Unreadable(RawEventBuilder.Origin)).Should().BeEmpty();
        decoder.Decode(RawEventBuilder.WithoutPlayer(RawEventBuilder.Origin)).Should().BeEmpty();
        var unknown = EventFixtures.Load("hero-created.json");
        unknown.EnvelopeVersion = 99;
        decoder
            .Decode(RawEventBuilder.FromEnvelope(unknown, RawEventBuilder.Origin))
            .Should()
            .BeEmpty();
        var valid = EventFixtures.Load("hero-created.json");
        decoder
            .Decode(
                RawEventBuilder.FromEnvelope(valid, RawEventBuilder.Origin) with
                {
                    ParseStatus = "InvalidOrUnknown",
                }
            )
            .Should()
            .BeEmpty();
    }
}
