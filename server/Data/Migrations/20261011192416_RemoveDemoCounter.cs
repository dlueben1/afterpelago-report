using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDemoCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemoCounters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemoCounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemoCounters", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "DemoCounters",
                columns: new[] { "Id", "UpdatedAt", "Value" },
                values: new object[] { 1, "2026-01-01T00:00:00.0000000Z", 0L });
        }
    }
}
