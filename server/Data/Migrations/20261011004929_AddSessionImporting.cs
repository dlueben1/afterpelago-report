using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionImporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    OriginalStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalEnd = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartUtc = table.Column<string>(type: "TEXT", nullable: true),
                    EndUtc = table.Column<string>(type: "TEXT", nullable: true),
                    SourceUtcOffsetMinutes = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionImports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ByteLength = table.Column<long>(type: "INTEGER", nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningDetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionImports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionImports_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PlayerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "BINARY"),
                    GameName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GoalCompleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Slots_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionLogEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportId = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceLineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTimestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    TeamNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SlotId = table.Column<int>(type: "INTEGER", nullable: true),
                    DataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionLogEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionLogEvents_SessionImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "SessionImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionLogEvents_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionLogEvents_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Checks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    SenderSlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiverSlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    LocationName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checks_SessionLogEvents_LogEventId",
                        column: x => x.LogEventId,
                        principalTable: "SessionLogEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Checks_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Checks_Slots_ReceiverSlotId",
                        column: x => x.ReceiverSlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Checks_Slots_SenderSlotId",
                        column: x => x.SenderSlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Hints",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    SenderSlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiverSlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemName = table.Column<string>(type: "TEXT", nullable: false),
                    LocationName = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hints_SessionLogEvents_LogEventId",
                        column: x => x.LogEventId,
                        principalTable: "SessionLogEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Hints_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Hints_Slots_ReceiverSlotId",
                        column: x => x.ReceiverSlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Hints_Slots_SenderSlotId",
                        column: x => x.SenderSlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SlotConnectionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotId = table.Column<int>(type: "INTEGER", nullable: false),
                    LogEventId = table.Column<long>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ClientVersion = table.Column<string>(type: "TEXT", nullable: true),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlotConnectionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SlotConnectionEvents_SessionLogEvents_LogEventId",
                        column: x => x.LogEventId,
                        principalTable: "SessionLogEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SlotConnectionEvents_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SlotConnectionEvents_Slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "Slots",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Checks_LogEventId",
                table: "Checks",
                column: "LogEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Checks_ReceiverSlotId",
                table: "Checks",
                column: "ReceiverSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Checks_SenderSlotId",
                table: "Checks",
                column: "SenderSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Checks_SessionId_ReceiverSlotId",
                table: "Checks",
                columns: new[] { "SessionId", "ReceiverSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Checks_SessionId_SenderSlotId",
                table: "Checks",
                columns: new[] { "SessionId", "SenderSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_Hints_LogEventId",
                table: "Hints",
                column: "LogEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hints_ReceiverSlotId",
                table: "Hints",
                column: "ReceiverSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Hints_SenderSlotId",
                table: "Hints",
                column: "SenderSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Hints_SessionId",
                table: "Hints",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionImports_RequestId",
                table: "SessionImports",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionImports_SessionId",
                table: "SessionImports",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionLogEvents_ImportId_SourceLineNumber",
                table: "SessionLogEvents",
                columns: new[] { "ImportId", "SourceLineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionLogEvents_SessionId_TimestampUtc_SourceLineNumber",
                table: "SessionLogEvents",
                columns: new[] { "SessionId", "TimestampUtc", "SourceLineNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionLogEvents_SlotId",
                table: "SessionLogEvents",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_State_StartUtc",
                table: "Sessions",
                columns: new[] { "State", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SlotConnectionEvents_LogEventId",
                table: "SlotConnectionEvents",
                column: "LogEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SlotConnectionEvents_SessionId",
                table: "SlotConnectionEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SlotConnectionEvents_SlotId",
                table: "SlotConnectionEvents",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_Slots_SessionId_TeamNumber_PlayerName",
                table: "Slots",
                columns: new[] { "SessionId", "TeamNumber", "PlayerName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Checks");

            migrationBuilder.DropTable(
                name: "Hints");

            migrationBuilder.DropTable(
                name: "SlotConnectionEvents");

            migrationBuilder.DropTable(
                name: "SessionLogEvents");

            migrationBuilder.DropTable(
                name: "SessionImports");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
