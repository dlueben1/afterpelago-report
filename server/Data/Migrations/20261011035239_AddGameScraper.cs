using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameScraper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameId",
                table: "Slots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "BINARY"),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameImageAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Hash = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    ByteLength = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameImageAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameImageAssets_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameItems_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameScrapes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameId = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpectedRevision = table.Column<int>(type: "INTEGER", nullable: true),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", nullable: false),
                    ManifestJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CommitResponseJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameScrapes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameScrapes_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameImageCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameId = table.Column<int>(type: "INTEGER", nullable: false),
                    AssetId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    CodesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ApIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SourceFile = table.Column<string>(type: "TEXT", nullable: false),
                    Variant = table.Column<string>(type: "TEXT", nullable: false),
                    PackUid = table.Column<string>(type: "TEXT", nullable: false),
                    PackVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Modifiers = table.Column<string>(type: "TEXT", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: true),
                    AutomaticEligible = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameImageCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameImageCandidates_GameImageAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "GameImageAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameImageCandidates_GameScrapes_ImportId",
                        column: x => x.ImportId,
                        principalTable: "GameScrapes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameImageCandidates_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GameItemImages",
                columns: table => new
                {
                    GameItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    CandidateId = table.Column<int>(type: "INTEGER", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameItemImages", x => x.GameItemId);
                    table.ForeignKey(
                        name: "FK_GameItemImages_GameImageCandidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "GameImageCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameItemImages_GameItems_GameItemId",
                        column: x => x.GameItemId,
                        principalTable: "GameItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_GameId",
                table: "Slots",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameImageAssets_GameId_Hash",
                table: "GameImageAssets",
                columns: new[] { "GameId", "Hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameImageCandidates_AssetId",
                table: "GameImageCandidates",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_GameImageCandidates_GameId_SourceKey",
                table: "GameImageCandidates",
                columns: new[] { "GameId", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameImageCandidates_ImportId",
                table: "GameImageCandidates",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_GameItemImages_CandidateId",
                table: "GameItemImages",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_GameItems_GameId_Name",
                table: "GameItems",
                columns: new[] { "GameId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameScrapes_CreatedAt",
                table: "GameScrapes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_GameScrapes_GameId",
                table: "GameScrapes",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Name",
                table: "Games",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Slots_Games_GameId",
                table: "Slots",
                column: "GameId",
                principalTable: "Games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Canonical game/item identities come from imported logs, never from tracker display names.
            migrationBuilder.Sql("""
                INSERT INTO Games (Name, Revision)
                SELECT DISTINCT GameName COLLATE BINARY, 0 FROM Slots
                WHERE GameName IS NOT NULL AND trim(GameName) <> '' ORDER BY GameName COLLATE BINARY;

                UPDATE Slots SET GameId = (SELECT Id FROM Games WHERE Games.Name = Slots.GameName COLLATE BINARY);

                INSERT INTO GameItems (GameId, Name)
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
            migrationBuilder.DropForeignKey(
                name: "FK_Slots_Games_GameId",
                table: "Slots");

            migrationBuilder.DropTable(
                name: "GameItemImages");

            migrationBuilder.DropTable(
                name: "GameImageCandidates");

            migrationBuilder.DropTable(
                name: "GameItems");

            migrationBuilder.DropTable(
                name: "GameImageAssets");

            migrationBuilder.DropTable(
                name: "GameScrapes");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropIndex(
                name: "IX_Slots_GameId",
                table: "Slots");

            migrationBuilder.DropColumn(
                name: "GameId",
                table: "Slots");
        }
    }
}
