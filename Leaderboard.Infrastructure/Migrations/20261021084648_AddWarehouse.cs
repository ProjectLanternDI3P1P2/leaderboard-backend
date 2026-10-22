using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leaderboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warehouse_player_action_statistics",
                columns: table => new
                {
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    action_type = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    action_count = table.Column<long>(type: "bigint", nullable: false),
                    value_total = table.Column<decimal>(type: "numeric", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_warehouse_player_action_statistics",
                        x => new
                        {
                            x.generation_id,
                            x.player_id,
                            x.action_type,
                        }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "warehouse_player_statistics",
                columns: table => new
                {
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false,
                        collation: "C"
                    ),
                    total_actions = table.Column<long>(type: "bigint", nullable: false),
                    value_total = table.Column<decimal>(type: "numeric", nullable: false),
                    last_action_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_warehouse_player_statistics",
                        x => new { x.generation_id, x.player_id }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "warehouse_processed_messages",
                columns: table => new
                {
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_warehouse_processed_messages",
                        x => new { x.generation_id, x.message_id }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "warehouse_rebuilds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(
                        type: "character varying(16)",
                        maxLength: 16,
                        nullable: false
                    ),
                    requested_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    completed_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    consolidated_until = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    events_processed = table.Column<long>(type: "bigint", nullable: false),
                    events_applied = table.Column<long>(type: "bigint", nullable: false),
                    duplicates_ignored = table.Column<long>(type: "bigint", nullable: false),
                    events_ignored = table.Column<long>(type: "bigint", nullable: false),
                    latest_event_received_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    cursor_event_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(
                        type: "character varying(1024)",
                        maxLength: 1024,
                        nullable: true
                    ),
                    updated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_rebuilds", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "warehouse_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    active_generation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_generation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    consolidated_until = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouse_state", x => x.id);
                }
            );

            migrationBuilder.InsertData(
                table: "warehouse_state",
                columns: new[]
                {
                    "id",
                    "activated_at",
                    "active_generation_id",
                    "consolidated_until",
                    "previous_generation_id",
                },
                values: new object[] { 1, null, null, null, null }
            );

            migrationBuilder.CreateIndex(
                name: "ix_warehouse_player_statistics_rank",
                table: "warehouse_player_statistics",
                columns: new[] { "generation_id", "total_actions", "player_id" },
                descending: new[] { false, true, false }
            );

            migrationBuilder.CreateIndex(
                name: "ux_warehouse_rebuilds_single_active",
                table: "warehouse_rebuilds",
                column: "status",
                unique: true,
                filter: "status IN ('Pending', 'Running')"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "warehouse_player_action_statistics");

            migrationBuilder.DropTable(name: "warehouse_player_statistics");

            migrationBuilder.DropTable(name: "warehouse_processed_messages");

            migrationBuilder.DropTable(name: "warehouse_rebuilds");

            migrationBuilder.DropTable(name: "warehouse_state");
        }
    }
}
