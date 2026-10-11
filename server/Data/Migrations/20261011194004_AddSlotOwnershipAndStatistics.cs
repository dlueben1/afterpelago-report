using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotOwnershipAndStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerDiscordUserId",
                table: "Slots",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ownership",
                table: "Slots",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Unclaimed");

            migrationBuilder.AddColumn<string>(
                name: "OwnershipSource",
                table: "Slots",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipUpdatedAt",
                table: "Slots",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionReviews",
                columns: table => new
                {
                    DiscordUserId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedAt = table.Column<string>(type: "TEXT", nullable: false),
                    NotInSession = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionReviews", x => new { x.DiscordUserId, x.SessionId });
                    table.ForeignKey(
                        name: "FK_SessionReviews_AccessRecords_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "AccessRecords",
                        principalColumn: "DiscordUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionReviews_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionStatistics",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionStatistics", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_SessionStatistics_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlotStatistics",
                columns: table => new
                {
                    SlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotStatistics", x => x.SlotId);
                    table.ForeignKey(
                        name: "FK_SlotStatistics_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserStatistics",
                columns: table => new
                {
                    DiscordUserId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EngineVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStatistics", x => x.DiscordUserId);
                    table.ForeignKey(
                        name: "FK_UserStatistics_AccessRecords_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "AccessRecords",
                        principalColumn: "DiscordUserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_OwnerDiscordUserId",
                table: "Slots",
                column: "OwnerDiscordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionReviews_SessionId",
                table: "SessionReviews",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotStatistics_SessionId",
                table: "SlotStatistics",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Slots_AccessRecords_OwnerDiscordUserId",
                table: "Slots",
                column: "OwnerDiscordUserId",
                principalTable: "AccessRecords",
                principalColumn: "DiscordUserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Slots_AccessRecords_OwnerDiscordUserId",
                table: "Slots");

            migrationBuilder.DropTable(
                name: "SessionReviews");

            migrationBuilder.DropTable(
                name: "SessionStatistics");

            migrationBuilder.DropTable(
                name: "SlotStatistics");

            migrationBuilder.DropTable(
                name: "UserStatistics");

            migrationBuilder.DropIndex(
                name: "IX_Slots_OwnerDiscordUserId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "OwnerDiscordUserId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "Ownership",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "OwnershipSource",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "OwnershipUpdatedAt",
                table: "Slots");
        }
    }
}
