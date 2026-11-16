using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwoNineteenVolts.Venue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Spots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    NameKey = table.Column<string>(type: "TEXT", nullable: false),
                    HourlyRateTetri = table.Column<long>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Spots_GroupId",
                table: "Spots",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_NameKey",
                table: "Groups",
                column: "NameKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Spots_Groups_GroupId",
                table: "Spots",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Spots_Groups_GroupId",
                table: "Spots");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Spots_GroupId",
                table: "Spots");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Spots");
        }
    }
}
