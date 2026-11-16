using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwoNineteenVolts.Venue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessions : Migration
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
                    SpotId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    StartedByName = table.Column<string>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndReason = table.Column<string>(type: "TEXT", nullable: true),
                    EndedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    EndedByName = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Spots_SpotId",
                        column: x => x.SpotId,
                        principalTable: "Spots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionCharges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", nullable: false),
                    Minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedTetri = table.Column<long>(type: "INTEGER", nullable: false),
                    ChargedTetri = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCharges_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_At",
                table: "SessionCharges",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharges_SessionId",
                table: "SessionCharges",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_EndsAt",
                table: "Sessions",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SpotId",
                table: "Sessions",
                column: "SpotId",
                unique: true,
                filter: "\"EndedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionCharges");

            migrationBuilder.DropTable(
                name: "Sessions");
        }
    }
}
