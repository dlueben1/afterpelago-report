using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionStatusAndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Sessions",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Finished");

            migrationBuilder.CreateTable(
                name: "SessionFavorites",
                columns: table => new
                {
                    DiscordUserId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionFavorites", x => new { x.DiscordUserId, x.SessionId });
                    table.ForeignKey(
                        name: "FK_SessionFavorites_AccessRecords_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "AccessRecords",
                        principalColumn: "DiscordUserId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionFavorites_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionFavorites_SessionId",
                table: "SessionFavorites",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionFavorites");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sessions");
        }
    }
}
