using System.Text.Json;
using FluentAssertions;
using Google.Protobuf;
using Leaderboard.Contracts.Collection.V1;

namespace Leaderboard.Test.Collection;

public class ContractFixtureTests
{
    private static readonly string FixtureDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "CollectionFixtures"
    );
    private static readonly JsonElement Manifest = ReadManifest();

    public static IEnumerable<object[]> FixtureCases() =>
        Manifest
            .GetProperty("fixtures")
            .EnumerateArray()
            .Select(x =>
                new object[]
                {
                    x.GetProperty("id").GetString()!,
                    x.GetProperty("expected").GetString()!,
                }
            );

    [Theory]
    [MemberData(nameof(FixtureCases))]
    public void Fixture_HasExpectedValidityAndCanGenerateRealProtobuf(string id, string expected)
    {
        // Arrange
        var fixture = Entry(id);
        fixture.GetProperty("status").GetString().Should().Be("proposed");

        if (expected == "unreadable")
        {
            var bytes = Convert.FromBase64String(fixture.GetProperty("bodyBase64").GetString()!);
            // Act / Assert
            var parse = () => GameEventEnvelope.Parser.ParseFrom(bytes);
            parse.Should().Throw<InvalidProtocolBufferException>();
            return;
        }

        var message = Message(id);

        // Act
        var bytesFromGeneratedType = message.ToByteArray();
        var parsed = GameEventEnvelope.Parser.ParseFrom(bytesFromGeneratedType);
        var errors = GameEventContract.Validate(parsed);

        // Assert
        parsed.Should().Be(message);
        if (expected == "valid")
        {
            errors.Should().BeEmpty($"fixture {id} must satisfy the proposed contract");
            GameEventContract
                .Events.Single(x => x.EventType == parsed.EventType)
                .RoutingKey.Should()
                .Be(fixture.GetProperty("routingKey").GetString());
        }
        else
            errors.Should().NotBeEmpty($"fixture {id} deliberately violates contract rules");
    }

    [Fact]
    public void Manifest_CoversEveryCatalogueEventAndEverySelectedPayload()
    {
        // Arrange
        string[] expectedRoutingKeys =
        [
            "dungeon.dungeon-run-started.v1",
            "dungeon.floor-entered.v1",
            "dungeon.dungeon-run-ended.v1",
            "combat.combat-started.v1",
            "combat.skill-used.v1",
            "combat.combat-ended.v1",
            "combat.consumable-used.v1",
            "combat.combat-completed.v1",
            "player.hero-created.v1",
            "player.game-session-participant-joined.v1",
            "player.game-session-participant-left.v1",
            "rewards.item-granted.v1",
            "rewards.item-equipped.v1",
            "rewards.gold-earned.v1",
            "progression.xp-awarded.v1",
            "progression.level-up.v1",
        ];

        // Act
        var validMessages = Manifest
            .GetProperty("fixtures")
            .EnumerateArray()
            .Where(x => x.GetProperty("expected").GetString() == "valid")
            .Select(x => Message(x.GetProperty("id").GetString()!))
            .ToArray();

        // Assert
        Manifest
            .GetProperty("contract")
            .GetString()
            .Should()
            .Be(GameEventEnvelope.Descriptor.FullName);
        Manifest.GetProperty("producerAdoption").GetString().Should().Be("not-asserted");
        GameEventContract
            .Events.Select(x => x.RoutingKey)
            .Should()
            .BeEquivalentTo(expectedRoutingKeys);
        validMessages
            .Select(x => $"{x.EventType}.v{x.EventVersion}")
            .Distinct()
            .Should()
            .BeEquivalentTo(expectedRoutingKeys);
        validMessages
            .Select(x => x.PayloadCase)
            .Distinct()
            .Should()
            .BeEquivalentTo(
                Enum.GetValues<GameEventEnvelope.PayloadOneofCase>()
                    .Where(x => x != GameEventEnvelope.PayloadOneofCase.None)
            );
    }

    [Fact]
    public void Participants_IncludeCreatorSecondHeroAndClanSnapshot()
    {
        // Arrange / Act
        var creator = Message("participant-creator").ParticipantJoined;
        var second = Message("participant-second").ParticipantJoined;
        var started = Message("run-started").DungeonRunStarted;
        var earlier = Message("skill-positive").SkillUsed.Actor;
        var later = Message("clan-later").SkillUsed.Actor;

        // Assert
        creator.HasIsCreator.Should().BeTrue();
        creator.IsCreator.Should().BeTrue();
        second.HasIsCreator.Should().BeTrue();
        second.IsCreator.Should().BeFalse();
        creator.GameSessionId.Should().Be(second.GameSessionId);
        creator.Actor.PlayerId.Should().NotBe(second.Actor.PlayerId);
        creator.Actor.HeroId.Should().NotBe(second.Actor.HeroId);
        started.Participants.Should().BeEquivalentTo(new[] { creator.Actor, second.Actor });
        second.Actor.HasClanIdAtOccurrence.Should().BeFalse();
        earlier.HeroId.Should().Be(later.HeroId);
        earlier.ClanIdAtOccurrence.Should().NotBe(later.ClanIdAtOccurrence);
        earlier.ClanIdAtOccurrence.Should().Be(creator.Actor.ClanIdAtOccurrence);
    }

    [Fact]
    public void RepeatedFloorPair_IsTwoDistinctAppliedTransitions()
    {
        // Arrange / Act
        var first = Message("floor-first");
        var repeated = Message("floor-repeat");

        // Assert
        first.FloorEntered.FromFloorNumber.Should().Be(1);
        first.FloorEntered.ToFloorNumber.Should().Be(2);
        repeated.FloorEntered.FromFloorNumber.Should().Be(first.FloorEntered.FromFloorNumber);
        repeated.FloorEntered.ToFloorNumber.Should().Be(first.FloorEntered.ToFloorNumber);
        repeated.FloorEntered.TransitionId.Should().NotBe(first.FloorEntered.TransitionId);
        repeated.MessageId.Should().NotBe(first.MessageId);
        repeated.OccurredAt.Should().BeGreaterThan(first.OccurredAt);
    }

    [Fact]
    public void CombatResults_PreserveMonsterDetailsAndCompletionPrerequisites()
    {
        // Arrange / Act
        var start = Message("combat-started").CombatStarted;
        var won = Message("combat-ended-won").CombatEnded;
        var lost = Message("combat-ended-lost").CombatEnded;
        var completedWin = Message("combat-completed-won").CombatCompleted;
        var completedLoss = Message("combat-completed-lost").CombatCompleted;

        // Assert
        start
            .Monsters.Select(x => x.Rank)
            .Should()
            .BeEquivalentTo(new[] { MonsterRank.Normal, MonsterRank.Boss });
        start
            .Monsters.Should()
            .OnlyContain(x => x.Level > 0 && x.MonsterInstanceId != x.MonsterTypeId);
        won.MonsterResults.Select(x => x.Monster).Should().BeEquivalentTo(start.Monsters);
        won.Outcome.Should().Be(CombatOutcome.Won);
        lost.Outcome.Should().Be(CombatOutcome.Lost);
        completedWin.Result.Should().Be(won);
        completedLoss.Result.Should().Be(lost);
        completedWin.DungeonAcceptanceId.Should().NotBeEmpty();
        completedLoss.DungeonAcceptanceId.Should().NotBeEmpty();
        completedWin.HasRewardsConfirmationId.Should().BeTrue();
        completedLoss.HasRewardsConfirmationId.Should().BeFalse();
        lost.MonsterResults.Should().Contain(x => x.Outcome == MonsterOutcome.Survived);
        Message("combat-completed-won")
            .MessageId.Should()
            .NotBe(Message("combat-ended-won").MessageId);
    }

    [Fact]
    public void SkillExecution_DistinguishesExplicitZeroMissingValueAndDodge()
    {
        // Arrange / Act
        var positive = Message("skill-positive").SkillUsed;
        var zero = Message("skill-zero").SkillUsed;
        var absent = Message("missing-mana").SkillUsed;
        var dodged = Message("skill-dodged").SkillUsed;

        // Assert
        positive.ManaSpent.Should().BeGreaterThan(0);
        zero.ManaSpent.Should().Be(0);
        absent.ManaSpent.Should().Be(0);
        zero.HasManaSpent.Should().BeTrue();
        absent.HasManaSpent.Should().BeFalse();
        dodged.Targets.Should().Contain(x => x.Resolution == TargetResolution.Dodged);
        dodged.CommandId.Should().NotBe(positive.CommandId);
        GameEventContract
            .Validate(Message("missing-mana"))
            .Should()
            .Contain(x => x.Contains("mana_spent"));
    }

    [Fact]
    public void RewardsAndProgression_AreTypedAndCarryPersistedAmountsAndSources()
    {
        // Arrange / Act
        var stackable = Message("item-stackable").ItemGranted;
        var instanced = Message("item-instanced").ItemGranted;
        var equipped = Message("item-equipped").ItemEquipped;
        var xp = Message("xp-awarded").XpAwarded;
        var level = Message("level-up").LevelUp;

        // Assert
        stackable.Item.HasItemInstanceId.Should().BeFalse();
        stackable.Quantity.Should().BeGreaterThan(1);
        instanced.Item.HasItemInstanceId.Should().BeTrue();
        instanced.Quantity.Should().Be(1);
        equipped.ItemInstanceId.Should().Be(instanced.Item.ItemInstanceId);
        equipped.HasPreviousItemInstanceId.Should().BeTrue();
        Message("item-equipped-empty-slot")
            .ItemEquipped.HasPreviousItemInstanceId.Should()
            .BeFalse();
        stackable.Source.Type.Should().Be(RewardSourceType.Combat);
        stackable.Source.SourceId.Should().Be(Message("combat-ended-won").CombatEnded.CombatId);
        Message("gold-player-wallet")
            .GoldEarned.WalletOwnerCase.Should()
            .Be(GoldEarned.WalletOwnerOneofCase.PlayerWalletId);
        Message("gold-hero-wallet")
            .GoldEarned.WalletOwnerCase.Should()
            .Be(GoldEarned.WalletOwnerOneofCase.HeroWalletId);
        xp.TotalXpAfter.Should().BeGreaterThanOrEqualTo(xp.Amount);
        level.XpAwardId.Should().Be(xp.XpAwardId);
        level.NewLevel.Should().BeGreaterThan(level.PreviousLevel + 1);
    }

    [Fact]
    public void DeliveryScenarios_SeparateTransactionMessageAndReceiptIdentitiesAndDates()
    {
        // Arrange
        var sharedTransaction = Scenario("same-transaction")
            .GetProperty("deliveries")
            .EnumerateArray()
            .ToArray();
        var redelivery = Scenario("redelivery")
            .GetProperty("deliveries")
            .EnumerateArray()
            .ToArray();
        var historical = Scenario("historical-recent-receipt").GetProperty("deliveries")[0];

        // Act
        var first = DeliveredMessage(sharedTransaction[0]);
        var second = DeliveredMessage(sharedTransaction[1]);

        // Assert: this validates the test inputs, not a collector or a repository.
        first.TransactionId.Should().Be(second.TransactionId);
        first.MessageId.Should().NotBe(second.MessageId);
        first.ToByteArray().Should().NotEqual(second.ToByteArray());
        sharedTransaction[0]
            .GetProperty("receivedAt")
            .GetDateTimeOffset()
            .Should()
            .Be(sharedTransaction[1].GetProperty("receivedAt").GetDateTimeOffset());
        DeliveredMessage(redelivery[0])
            .ToByteArray()
            .Should()
            .Equal(DeliveredMessage(redelivery[1]).ToByteArray());
        redelivery[0]
            .GetProperty("receiptId")
            .GetString()
            .Should()
            .NotBe(redelivery[1].GetProperty("receiptId").GetString());
        DeliveredMessage(historical).OccurredAt.ToDateTimeOffset().Year.Should().Be(2025);
        historical.GetProperty("receivedAt").GetDateTimeOffset().Year.Should().Be(2026);
        Scenario("historical-receipt")
            .GetProperty("deliveries")[0]
            .GetProperty("receivedAt")
            .GetDateTimeOffset()
            .Should()
            .BeBefore(new DateTimeOffset(2025, 10, 20, 0, 0, 0, TimeSpan.Zero));
        GameEventEnvelope
            .Descriptor.Fields.InFieldNumberOrder()
            .Select(x => x.Name)
            .Should()
            .NotContain(new[] { "received_at", "receipt_id" });
    }

    [Fact]
    public void Reserialization_CanChangeValidWireBytesSoOriginalBodyMustBeArchived()
    {
        // Arrange: a repeated singular field is legal on the Protobuf wire.
        var message = Message("hero-created");
        var canonical = message.ToByteArray();
        var originalAmqpBody = new byte[] { 8, 1 }
            .Concat(canonical)
            .ToArray();

        // Act
        var parsed = GameEventEnvelope.Parser.ParseFrom(originalAmqpBody);

        // Assert
        parsed.Should().Be(message);
        GameEventContract.Validate(parsed).Should().BeEmpty();
        parsed.ToByteArray().Should().NotEqual(originalAmqpBody);
        originalAmqpBody.Should().HaveCount(canonical.Length + 2);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("00000000000040008000000000000001")]
    [InlineData("AAAAAAAA-0000-4000-8000-000000000001")]
    [InlineData("not-a-guid")]
    public void MessageIdentity_MustUseNonemptyCanonicalLowercaseGuid(string id)
    {
        // Arrange
        var message = Message("hero-created");
        message.MessageId = id;

        // Act / Assert
        GameEventContract.Validate(message).Should().Contain(x => x.Contains("message_id"));
    }

    [Fact]
    public void InvalidTimestampUnknownEnumAndDuplicateHeroes_AreReported()
    {
        // Arrange
        var message = Message("run-started");
        message.OccurredAt.Nanos = -1;
        message.ProducerSquad = (ProducerSquad)99;
        message.DungeonRunStarted.Participants.Add(
            message.DungeonRunStarted.Participants[0].Clone()
        );

        // Act
        var errors = GameEventContract.Validate(message);

        // Assert
        errors.Should().Contain(x => x.Contains("occurred_at"));
        errors.Should().Contain(x => x.Contains("producer_squad"));
        errors.Should().Contain(x => x.Contains("duplicate identities"));
    }

    [Fact]
    public void ExistingTemplateContract_RetainsItsOwnWireIdentity()
    {
        // Arrange / Act
        var original = Contracts.Events.V1.GameEventMessage.Descriptor;

        // Assert
        original.FullName.Should().Be("leaderboard.events.v1.GameEventMessage");
        original.FindFieldByNumber(1).Name.Should().Be("version");
        original.FindFieldByNumber(2).Name.Should().Be("transaction_id");
        original.FindFieldByNumber(9).Name.Should().Be("data");
        original.FullName.Should().NotBe(GameEventEnvelope.Descriptor.FullName);
    }

    private static JsonElement ReadManifest()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(FixtureDirectory, "manifest.json"))
        );
        return document.RootElement.Clone();
    }

    private static JsonElement Entry(string id) =>
        Manifest
            .GetProperty("fixtures")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetString() == id);

    private static GameEventEnvelope Message(string id) =>
        GameEventEnvelope.Parser.ParseJson(
            File.ReadAllText(
                Path.Combine(FixtureDirectory, Entry(id).GetProperty("file").GetString()!)
            )
        );

    private static JsonElement Scenario(string id) =>
        Manifest
            .GetProperty("deliveryScenarios")
            .EnumerateArray()
            .Single(x => x.GetProperty("id").GetString() == id);

    private static GameEventEnvelope DeliveredMessage(JsonElement delivery) =>
        Message(delivery.GetProperty("fixture").GetString()!);
}
