using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFieldsFromCompanyConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BackupFrequency",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "BlobConnectionString",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "BlobContainerName",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "FtpHost",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "FtpPassword",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "FtpUser",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "GoogleDriveClientId",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "GoogleDriveClientSecret",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "GoogleDriveRefreshToken",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "KeepLocalCopy",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "LocalBackupPath",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "OneDriveClientId",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "OneDriveClientSecret",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "OneDriveRefreshToken",
                table: "CompanyConfigs");

            migrationBuilder.DropColumn(
                name: "StorageType",
                table: "CompanyConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackupFrequency",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BlobConnectionString",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlobContainerName",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FtpHost",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FtpPassword",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FtpUser",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveClientId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveClientSecret",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveRefreshToken",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "KeepLocalCopy",
                table: "CompanyConfigs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LocalBackupPath",
                table: "CompanyConfigs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneDriveClientId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneDriveClientSecret",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OneDriveRefreshToken",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageType",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
