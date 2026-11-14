using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TwoNineteenVolts.Venue.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NameKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DisplayName",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Spots_Name",
                table: "Spots");

            migrationBuilder.DropIndex(
                name: "IX_Devices_Name",
                table: "Devices");

            migrationBuilder.AddColumn<string>(
                name: "DisplayNameKey",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "Spots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameKey",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Users SET DisplayNameKey = upper(DisplayName);");
            migrationBuilder.Sql("UPDATE Spots SET NameKey = upper(Name);");
            migrationBuilder.Sql("UPDATE Devices SET NameKey = upper(Name);");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayNameKey",
                table: "Users",
                column: "DisplayNameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spots_NameKey",
                table: "Spots",
                column: "NameKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_NameKey",
                table: "Devices",
                column: "NameKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_DisplayNameKey",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Spots_NameKey",
                table: "Spots");

            migrationBuilder.DropIndex(
                name: "IX_Devices_NameKey",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DisplayNameKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "Spots");

            migrationBuilder.DropColumn(
                name: "NameKey",
                table: "Devices");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DisplayName",
                table: "Users",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spots_Name",
                table: "Spots",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_Name",
                table: "Devices",
                column: "Name",
                unique: true);
        }
    }
}
