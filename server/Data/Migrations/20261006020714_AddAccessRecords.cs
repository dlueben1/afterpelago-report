using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessRecords",
                columns: table => new
                {
                    DiscordUserId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RequestedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessRecords", x => x.DiscordUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessRecords_Status",
                table: "AccessRecords",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessRecords");
        }
    }
}
