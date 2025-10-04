using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDatabaseSourceModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionString",
                table: "DatabaseSources");

            migrationBuilder.AddColumn<string>(
                name: "BackupName",
                table: "DatabaseSources",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DatabaseName",
                table: "DatabaseSources",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Password",
                table: "DatabaseSources",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Server",
                table: "DatabaseSources",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "UseWindowsAuth",
                table: "DatabaseSources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "User",
                table: "DatabaseSources",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackupName",
                table: "DatabaseSources");

            migrationBuilder.DropColumn(
                name: "DatabaseName",
                table: "DatabaseSources");

            migrationBuilder.DropColumn(
                name: "Password",
                table: "DatabaseSources");

            migrationBuilder.DropColumn(
                name: "Server",
                table: "DatabaseSources");

            migrationBuilder.DropColumn(
                name: "UseWindowsAuth",
                table: "DatabaseSources");

            migrationBuilder.DropColumn(
                name: "User",
                table: "DatabaseSources");

            migrationBuilder.AddColumn<string>(
                name: "ConnectionString",
                table: "DatabaseSources",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
