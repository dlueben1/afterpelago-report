using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afterpelago.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCoverArt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverHash",
                table: "Games",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverHeight",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverWidth",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ImportId",
                table: "GameImageCandidates",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverHash",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CoverHeight",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "CoverWidth",
                table: "Games");

            migrationBuilder.AlterColumn<Guid>(
                name: "ImportId",
                table: "GameImageCandidates",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
