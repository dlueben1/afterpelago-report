using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillImportedGameCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent repair for development databases that applied the schema while it was being authored.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO Games (Name, Revision)
                SELECT DISTINCT GameName COLLATE BINARY, 0 FROM Slots
                WHERE GameName IS NOT NULL AND trim(GameName) <> '' ORDER BY GameName COLLATE BINARY;
                UPDATE Slots SET GameId = (SELECT Id FROM Games WHERE Games.Name = Slots.GameName COLLATE BINARY)
                WHERE GameId IS NULL;
                INSERT OR IGNORE INTO GameItems (GameId, Name)
                SELECT s.GameId, c.ItemName COLLATE BINARY FROM Checks c JOIN Slots s ON s.Id = c.ReceiverSlotId
                WHERE s.GameId IS NOT NULL AND trim(c.ItemName) <> ''
                UNION
                SELECT s.GameId, h.ItemName COLLATE BINARY FROM Hints h JOIN Slots s ON s.Id = h.ReceiverSlotId
                WHERE s.GameId IS NOT NULL AND trim(h.ItemName) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
