using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leaderboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseHeroRankings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "warehouse_hero_rankings",
                columns: table => new
                {
                    generation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hero_id = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    player_id = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false
                    ),
                    hero_class_id = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true
                    ),
                    total_xp = table.Column<long>(type: "bigint", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    last_activity_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_warehouse_hero_rankings",
                        x => new { x.generation_id, x.hero_id }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_hero_rankings_generation_id_total_xp_hero_id",
                table: "warehouse_hero_rankings",
                columns: new[] { "generation_id", "total_xp", "hero_id" },
                descending: new[] { false, true, false }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "warehouse_hero_rankings");
        }
    }
}
