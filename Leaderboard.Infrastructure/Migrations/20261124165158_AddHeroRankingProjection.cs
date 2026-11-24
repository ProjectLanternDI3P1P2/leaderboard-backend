using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Leaderboard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroRankingProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hero_rankings",
                columns: table => new
                {
                    HeroId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HeroName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    HeroClass = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlayerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PlayerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AllTimeXp = table.Column<long>(type: "bigint", nullable: false),
                    SeasonXp = table.Column<long>(type: "bigint", nullable: true),
                    WeekXp = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Runs = table.Column<int>(type: "integer", nullable: false),
                    WinRate = table.Column<double>(type: "double precision", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SeededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPending = table.Column<bool>(type: "boolean", nullable: false),
                    RankDelta = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hero_rankings", x => x.HeroId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hero_rankings_AllTimeXp",
                table: "hero_rankings",
                column: "AllTimeXp");

            migrationBuilder.CreateIndex(
                name: "IX_hero_rankings_PlayerId",
                table: "hero_rankings",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hero_rankings");
        }
    }
}
