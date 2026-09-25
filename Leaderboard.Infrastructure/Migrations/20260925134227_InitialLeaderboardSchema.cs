using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Leaderboard.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialLeaderboardSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "classes",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "varchar", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_classes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "statistics",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar", nullable: false),
                counts_in_score = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                multiplier = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                is_sortable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_statistics", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "heroes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                id_player = table.Column<Guid>(type: "uuid", nullable: true),
                id_class = table.Column<int>(type: "integer", nullable: false),
                pseudo = table.Column<string>(type: "varchar", nullable: false),
                level = table.Column<int>(type: "integer", nullable: false),
                score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_heroes", x => x.id);
                table.ForeignKey(
                    name: "FK_heroes_classes_id_class",
                    column: x => x.id_class,
                    principalTable: "classes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "achievements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "varchar", nullable: false),
                id_statistic = table.Column<Guid>(type: "uuid", nullable: false),
                @operator = table.Column<string>(name: "operator", type: "varchar", nullable: false),
                threshold = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_achievements", x => x.id);
                table.ForeignKey(
                    name: "FK_achievements_statistics_id_statistic",
                    column: x => x.id_statistic,
                    principalTable: "statistics",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hero_statistics",
            columns: table => new
            {
                id_hero = table.Column<Guid>(type: "uuid", nullable: false),
                id_statistic = table.Column<Guid>(type: "uuid", nullable: false),
                value = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hero_statistics", x => new { x.id_hero, x.id_statistic });
                table.ForeignKey(
                    name: "FK_hero_statistics_heroes_id_hero",
                    column: x => x.id_hero,
                    principalTable: "heroes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_hero_statistics_statistics_id_statistic",
                    column: x => x.id_statistic,
                    principalTable: "statistics",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hero_achievements",
            columns: table => new
            {
                id_achievement = table.Column<Guid>(type: "uuid", nullable: false),
                id_hero = table.Column<Guid>(type: "uuid", nullable: false),
                obtained_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hero_achievements", x => new { x.id_achievement, x.id_hero });
                table.ForeignKey(
                    name: "FK_hero_achievements_achievements_id_achievement",
                    column: x => x.id_achievement,
                    principalTable: "achievements",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_hero_achievements_heroes_id_hero",
                    column: x => x.id_hero,
                    principalTable: "heroes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_achievements_id_statistic",
            table: "achievements",
            column: "id_statistic");

        migrationBuilder.CreateIndex(
            name: "IX_classes_name",
            table: "classes",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_hero_achievements_id_hero",
            table: "hero_achievements",
            column: "id_hero");

        migrationBuilder.CreateIndex(
            name: "IX_hero_statistics_id_statistic_value",
            table: "hero_statistics",
            columns: new[] { "id_statistic", "value" });

        migrationBuilder.CreateIndex(
            name: "IX_heroes_id_class_score",
            table: "heroes",
            columns: new[] { "id_class", "score" });

        migrationBuilder.CreateIndex(
            name: "IX_heroes_id_player",
            table: "heroes",
            column: "id_player");

        migrationBuilder.CreateIndex(
            name: "IX_heroes_score",
            table: "heroes",
            column: "score");

        migrationBuilder.CreateIndex(
            name: "IX_statistics_name",
            table: "statistics",
            column: "name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hero_achievements");

        migrationBuilder.DropTable(
            name: "hero_statistics");

        migrationBuilder.DropTable(
            name: "achievements");

        migrationBuilder.DropTable(
            name: "heroes");

        migrationBuilder.DropTable(
            name: "statistics");

        migrationBuilder.DropTable(
            name: "classes");
    }
}
