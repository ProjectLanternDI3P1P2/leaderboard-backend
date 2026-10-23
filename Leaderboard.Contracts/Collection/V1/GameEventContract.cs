using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace Leaderboard.Contracts.Collection.V1;

public sealed record EventDefinition(
    string EventType,
    ProducerSquad ProducerSquad,
    GameEventEnvelope.PayloadOneofCase PayloadCase
)
{
    public string RoutingKey => $"{EventType}.v1";
}

/// <summary>
/// Shape rules for the Leaderboard-owned ingestion contract. Validation never
/// authorizes discarding a received body: raw archival preserves invalid/unknown data too.
/// This does not verify the producer's persistence or publication guarantees.
/// </summary>
public static class GameEventContract
{
    public static IReadOnlyList<EventDefinition> Events { get; } =
        Array.AsReadOnly<EventDefinition>([
            new(
                "dungeon.dungeon-run-started",
                ProducerSquad.Dungeon,
                GameEventEnvelope.PayloadOneofCase.DungeonRunStarted
            ),
            new(
                "dungeon.floor-entered",
                ProducerSquad.Dungeon,
                GameEventEnvelope.PayloadOneofCase.FloorEntered
            ),
            new(
                "dungeon.dungeon-run-ended",
                ProducerSquad.Dungeon,
                GameEventEnvelope.PayloadOneofCase.DungeonRunEnded
            ),
            new(
                "combat.combat-started",
                ProducerSquad.Combat,
                GameEventEnvelope.PayloadOneofCase.CombatStarted
            ),
            new(
                "combat.skill-used",
                ProducerSquad.Combat,
                GameEventEnvelope.PayloadOneofCase.SkillUsed
            ),
            new(
                "combat.combat-ended",
                ProducerSquad.Combat,
                GameEventEnvelope.PayloadOneofCase.CombatEnded
            ),
            new(
                "combat.consumable-used",
                ProducerSquad.Combat,
                GameEventEnvelope.PayloadOneofCase.ConsumableUsed
            ),
            new(
                "combat.combat-completed",
                ProducerSquad.Combat,
                GameEventEnvelope.PayloadOneofCase.CombatCompleted
            ),
            new(
                "player.hero-created",
                ProducerSquad.Player,
                GameEventEnvelope.PayloadOneofCase.HeroCreated
            ),
            new(
                "player.game-session-participant-joined",
                ProducerSquad.Player,
                GameEventEnvelope.PayloadOneofCase.ParticipantJoined
            ),
            new(
                "player.game-session-participant-left",
                ProducerSquad.Player,
                GameEventEnvelope.PayloadOneofCase.ParticipantLeft
            ),
            new(
                "rewards.item-granted",
                ProducerSquad.Rewards,
                GameEventEnvelope.PayloadOneofCase.ItemGranted
            ),
            new(
                "rewards.item-equipped",
                ProducerSquad.Rewards,
                GameEventEnvelope.PayloadOneofCase.ItemEquipped
            ),
            new(
                "rewards.gold-earned",
                ProducerSquad.Rewards,
                GameEventEnvelope.PayloadOneofCase.GoldEarned
            ),
            new(
                "progression.xp-awarded",
                ProducerSquad.Progression,
                GameEventEnvelope.PayloadOneofCase.XpAwarded
            ),
            new(
                "progression.level-up",
                ProducerSquad.Progression,
                GameEventEnvelope.PayloadOneofCase.LevelUp
            ),
        ]);

    public static IReadOnlyList<string> Validate(GameEventEnvelope message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var errors = new List<string>();
        ValidateMessage(message, "event", errors);

        if (message.EnvelopeVersion != 1)
            errors.Add("event.envelope_version must be 1.");
        if (message.EventVersion != 1)
            errors.Add("event.event_version must be 1.");

        var definition = Events.SingleOrDefault(x => x.EventType == message.EventType);
        if (definition is null)
            errors.Add("event.event_type is unknown.");
        else
        {
            if (definition.ProducerSquad != message.ProducerSquad)
                errors.Add("event.producer_squad does not match event_type.");
            if (definition.PayloadCase != message.PayloadCase)
                errors.Add("event.payload does not match event_type.");
        }

        if (message.PayloadCase == GameEventEnvelope.PayloadOneofCase.None)
            errors.Add("event.payload is required.");

        var result = message.CombatEnded ?? message.CombatCompleted?.Result;
        if (
            result?.EndedAt is not null
            && message.OccurredAt is not null
            && IsTimestamp(result.EndedAt)
            && IsTimestamp(message.OccurredAt)
            && result.EndedAt.CompareTo(message.OccurredAt) > 0
        )
            errors.Add("event.occurred_at must not precede the combat end.");

        return errors.AsReadOnly();
    }

    private static void ValidateMessage(IMessage message, string path, List<string> errors)
    {
        if (message is Timestamp timestamp)
        {
            if (!IsTimestamp(timestamp))
                errors.Add($"{path} must be a normalized UTC timestamp.");
            return;
        }

        foreach (var field in message.Descriptor.Fields.InFieldNumberOrder())
        {
            var fieldPath = $"{path}.{field.Name}";
            // Real oneofs select one field; synthetic oneofs represent scalar presence.
            if (
                field.ContainingOneof is { IsSynthetic: false }
                && !field.Accessor.HasValue(message)
            )
                continue;

            var value = field.Accessor.GetValue(message);
            if (field.IsRepeated)
            {
                var children = ((System.Collections.IEnumerable)value).Cast<IMessage>().ToArray();
                if (children.Length == 0)
                    errors.Add($"{fieldPath} must not be empty.");
                for (var index = 0; index < children.Length; index++)
                    ValidateMessage(children[index], $"{fieldPath}[{index}]", errors);
                ValidateUnique(children, fieldPath, errors);
                continue;
            }

            if (field.FieldType == FieldType.Message)
            {
                if (value is IMessage child)
                    ValidateMessage(child, fieldPath, errors);
                else
                    errors.Add($"{fieldPath} is required.");
            }
            else if (field.FieldType == FieldType.String)
            {
                if (field.HasPresence && !field.Accessor.HasValue(message))
                    continue;
                if (field.Name == "event_type")
                {
                    if (string.IsNullOrEmpty((string)value))
                        errors.Add($"{fieldPath} is required.");
                }
                else if (!IsGuid((string)value))
                    errors.Add($"{fieldPath} must be a nonempty canonical lowercase GUID.");
            }
            else if (field.FieldType == FieldType.Enum)
            {
                var number = Convert.ToInt32(value);
                if (number == 0 || field.EnumType.FindValueByNumber(number) is null)
                    errors.Add($"{fieldPath} must be a known non-UNSPECIFIED value.");
            }
            else if (field.FieldType is FieldType.UInt32 or FieldType.UInt64 or FieldType.Bool)
            {
                if (field.HasPresence && !field.Accessor.HasValue(message))
                    errors.Add(
                        $"{fieldPath} is required (explicit zero/false is distinguishable from absence)."
                    );
                else if (
                    field.FieldType != FieldType.Bool
                    && Convert.ToUInt64(value) == 0
                    && field.Name is not ("mana_spent" or "turn_count" or "total_xp_after")
                )
                    errors.Add($"{fieldPath} must be positive.");
            }
        }

        switch (message)
        {
            case FloorEntered floor when floor.FromFloorNumber == floor.ToFloorNumber:
                errors.Add($"{path}.to_floor_number must differ from from_floor_number.");
                break;
            case SkillTarget target when target.TargetCase == SkillTarget.TargetOneofCase.None:
                errors.Add($"{path}.target is required.");
                break;
            case CombatResult result
                when result.StartedAt is not null
                    && result.EndedAt is not null
                    && IsTimestamp(result.StartedAt)
                    && IsTimestamp(result.EndedAt)
                    && result.StartedAt.CompareTo(result.EndedAt) > 0:
                errors.Add($"{path}.ended_at must not precede started_at.");
                break;
            case CombatCompleted completed when completed.Result is not null:
                if (
                    completed.Result.Outcome == CombatOutcome.Won
                    && !completed.HasRewardsConfirmationId
                )
                    errors.Add($"{path}.rewards_confirmation_id is required for a win.");
                if (
                    completed.Result.Outcome == CombatOutcome.Lost
                    && completed.HasRewardsConfirmationId
                )
                    errors.Add($"{path}.rewards_confirmation_id must be absent for a loss.");
                break;
            case ConsumableUsed used
                when used.Item?.HasItemInstanceId == true && used.Quantity != 1:
                errors.Add($"{path}.quantity must be 1 for an instanced item.");
                break;
            case ItemGranted granted
                when granted.Item?.HasItemInstanceId == true && granted.Quantity != 1:
                errors.Add($"{path}.quantity must be 1 for an instanced item.");
                break;
            case ItemEquipped equipped
                when equipped.HasPreviousItemInstanceId
                    && equipped.ItemInstanceId == equipped.PreviousItemInstanceId:
                errors.Add($"{path}.previous_item_instance_id must differ from item_instance_id.");
                break;
            case GoldEarned gold when gold.WalletOwnerCase == GoldEarned.WalletOwnerOneofCase.None:
                errors.Add($"{path}.wallet_owner is required.");
                break;
            case XpAwarded xp when xp.TotalXpAfter < xp.Amount:
                errors.Add($"{path}.total_xp_after must be at least amount.");
                break;
            case LevelUp level when level.NewLevel <= level.PreviousLevel:
                errors.Add($"{path}.new_level must exceed previous_level.");
                break;
        }
    }

    private static void ValidateUnique(IMessage[] children, string path, List<string> errors)
    {
        var identities = children
            .Select(child =>
                child switch
                {
                    Actor actor => actor.HeroId,
                    MonsterSnapshot monster => monster.MonsterInstanceId,
                    MonsterResult result => result.Monster?.MonsterInstanceId,
                    SkillTarget target =>
                        $"{target.TargetCase}:{(target.TargetCase == SkillTarget.TargetOneofCase.HeroId ? target.HeroId : target.MonsterInstanceId)}",
                    _ => null,
                }
            )
            .Where(x => x is not null)
            .ToArray();
        if (identities.Distinct(StringComparer.Ordinal).Count() != identities.Length)
            errors.Add($"{path} contains duplicate identities.");
    }

    private static bool IsGuid(string value) =>
        Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && value == id.ToString("D");

    private static bool IsTimestamp(Timestamp timestamp) =>
        timestamp.Seconds is >= -62135596800L and <= 253402300799L
        && timestamp.Nanos is >= 0 and <= 999999999;
}
