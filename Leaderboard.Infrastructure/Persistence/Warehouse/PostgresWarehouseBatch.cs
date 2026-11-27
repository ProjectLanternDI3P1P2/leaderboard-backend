using System.Data.Common;
using Leaderboard.Domain.Repositories;
using Leaderboard.Domain.ValueObjects;
using Leaderboard.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Leaderboard.Infrastructure.Persistence.Warehouse;

/// <summary>
/// One batch written with set-based statements: a whole batch costs three round trips,
/// whatever its size, which keeps a 5 million event rebuild within its time budget.
/// </summary>
public sealed class PostgresWarehouseBatch(
    LeaderboardDbContext dbContext,
    IDbContextTransaction transaction,
    Guid generationId
) : IWarehouseBatch
{
    private const string RegisterSql = $"""
        INSERT INTO {WarehouseProcessedMessageConfiguration.TableName} (generation_id, message_id)
        SELECT @generation_id, unnest(@message_ids)
        ON CONFLICT DO NOTHING
        RETURNING message_id
        """;

    private const string ApplyPlayersSql = $"""
        INSERT INTO {PlayerStatisticsConfiguration.TableName} AS s
            (generation_id, player_id, total_actions, last_action_at)
        SELECT @generation_id, d.player_id, d.total_actions, d.last_action_at
        FROM unnest(@player_ids, @total_actions, @last_action_ats)
            AS d(player_id, total_actions, last_action_at)
        ON CONFLICT (generation_id, player_id) DO UPDATE SET
            total_actions = s.total_actions + excluded.total_actions,
            last_action_at = GREATEST(s.last_action_at, excluded.last_action_at)
        """;

    private const string ApplyActionsSql = $"""
        INSERT INTO {PlayerActionStatisticsConfiguration.TableName} AS s
            (generation_id, player_id, action_type, action_count, value_total)
        SELECT @generation_id, d.player_id, d.action_type, d.action_count, d.value_total
        FROM unnest(@player_ids, @action_types, @action_counts, @value_totals)
            AS d(player_id, action_type, action_count, value_total)
        ON CONFLICT (generation_id, player_id, action_type) DO UPDATE SET
            action_count = s.action_count + excluded.action_count,
            value_total = s.value_total + excluded.value_total
        """;

    private const string ApplyHeroesSql = $"""
        INSERT INTO {HeroRankingSnapshotConfiguration.TableName} AS s
            (generation_id, hero_id, player_id, hero_class_id, total_xp, level, first_seen_at, last_activity_at)
        SELECT @generation_id, d.hero_id, d.player_id, d.hero_class_id,
            coalesce(d.total_xp, 0), coalesce(d.level, 1), d.first_seen_at, d.last_activity_at
        FROM unnest(@hero_ids, @hero_player_ids, @hero_class_ids, @hero_total_xps,
            @hero_levels, @first_seen_ats, @hero_last_activity_ats)
            AS d(hero_id, player_id, hero_class_id, total_xp, level, first_seen_at, last_activity_at)
        ON CONFLICT (generation_id, hero_id) DO UPDATE SET
            player_id = excluded.player_id,
            hero_class_id = coalesce(excluded.hero_class_id, s.hero_class_id),
            total_xp = greatest(s.total_xp, excluded.total_xp),
            level = greatest(s.level, excluded.level),
            first_seen_at = least(s.first_seen_at, excluded.first_seen_at),
            last_activity_at = greatest(s.last_activity_at, excluded.last_activity_at)
        """;

    public async Task<IReadOnlySet<Guid>> RegisterMessagesAsync(
        IReadOnlyCollection<Guid> messageIds,
        CancellationToken cancellationToken
    )
    {
        HashSet<Guid> registered = [];
        if (messageIds.Count == 0)
        {
            return registered;
        }

        await using NpgsqlCommand command = CreateCommand(RegisterSql);
        command.Parameters.Add(Uuid("generation_id", generationId));
        command.Parameters.Add(Array("message_ids", NpgsqlDbType.Uuid, messageIds.ToArray()));

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            registered.Add(reader.GetGuid(0));
        }

        return registered;
    }

    public async Task ApplyAsync(WarehouseDelta delta, CancellationToken cancellationToken)
    {
        if (delta.Players.Count > 0)
        {
            await using NpgsqlCommand players = CreateCommand(ApplyPlayersSql);
            players.Parameters.Add(Uuid("generation_id", generationId));
            players.Parameters.Add(
                Array(
                    "player_ids",
                    NpgsqlDbType.Text,
                    delta.Players.Select(p => p.PlayerId).ToArray()
                )
            );
            players.Parameters.Add(
                Array(
                    "total_actions",
                    NpgsqlDbType.Bigint,
                    delta.Players.Select(p => p.TotalActions).ToArray()
                )
            );
            players.Parameters.Add(
                Array(
                    "last_action_ats",
                    NpgsqlDbType.TimestampTz,
                    delta.Players.Select(p => p.LastActionAt.UtcDateTime).ToArray()
                )
            );
            await players.ExecuteNonQueryAsync(cancellationToken);
        }

        if (delta.Actions.Count > 0)
        {
            await using NpgsqlCommand actions = CreateCommand(ApplyActionsSql);
            actions.Parameters.Add(Uuid("generation_id", generationId));
            actions.Parameters.Add(
                Array(
                    "player_ids",
                    NpgsqlDbType.Text,
                    delta.Actions.Select(a => a.PlayerId).ToArray()
                )
            );
            actions.Parameters.Add(
                Array(
                    "action_types",
                    NpgsqlDbType.Text,
                    delta.Actions.Select(a => a.ActionType).ToArray()
                )
            );
            actions.Parameters.Add(
                Array(
                    "action_counts",
                    NpgsqlDbType.Bigint,
                    delta.Actions.Select(a => a.ActionCount).ToArray()
                )
            );
            actions.Parameters.Add(
                Array(
                    "value_totals",
                    NpgsqlDbType.Numeric,
                    delta.Actions.Select(a => a.ValueTotal).ToArray()
                )
            );
            await actions.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task ApplyHeroRankingsAsync(
        IReadOnlyList<HeroRankingDelta> changes,
        CancellationToken cancellationToken
    )
    {
        if (changes.Count == 0)
            return;

        await using NpgsqlCommand heroes = CreateCommand(ApplyHeroesSql);
        heroes.Parameters.Add(Uuid("generation_id", generationId));
        heroes.Parameters.Add(
            Array("hero_ids", NpgsqlDbType.Text, changes.Select(h => h.HeroId).ToArray())
        );
        heroes.Parameters.Add(
            Array("hero_player_ids", NpgsqlDbType.Text, changes.Select(h => h.PlayerId).ToArray())
        );
        heroes.Parameters.Add(
            Array("hero_class_ids", NpgsqlDbType.Text, changes.Select(h => h.HeroClassId).ToArray())
        );
        heroes.Parameters.Add(
            Array(
                "hero_total_xps",
                NpgsqlDbType.Bigint,
                changes.Select(h => h.TotalXpAfter).ToArray()
            )
        );
        heroes.Parameters.Add(
            Array("hero_levels", NpgsqlDbType.Integer, changes.Select(h => h.Level).ToArray())
        );
        heroes.Parameters.Add(
            Array(
                "first_seen_ats",
                NpgsqlDbType.TimestampTz,
                changes.Select(h => h.FirstSeenAt.UtcDateTime).ToArray()
            )
        );
        heroes.Parameters.Add(
            Array(
                "hero_last_activity_ats",
                NpgsqlDbType.TimestampTz,
                changes.Select(h => h.LastActivityAt.UtcDateTime).ToArray()
            )
        );
        await heroes.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        // The tracked rebuild carries the checkpoint: saved in the same transaction.
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => transaction.DisposeAsync();

    private NpgsqlCommand CreateCommand(string sql) =>
        new(sql, (NpgsqlConnection)dbContext.Database.GetDbConnection())
        {
            Transaction = (NpgsqlTransaction)transaction.GetDbTransaction(),
        };

    private static NpgsqlParameter Uuid(string name, Guid value) =>
        new(name, NpgsqlDbType.Uuid) { Value = value };

    private static NpgsqlParameter Array<T>(string name, NpgsqlDbType elementType, T[] values) =>
        new(name, NpgsqlDbType.Array | elementType) { Value = values };
}
