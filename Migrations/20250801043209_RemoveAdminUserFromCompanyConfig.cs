using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAdminUserFromCompanyConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminUserName",
                table: "CompanyConfigs");

            migrationBuilder.RenameColumn(
                name: "StorageCredentials",
                table: "CompanyConfigs",
                newName: "OneDriveRefreshToken");

            migrationBuilder.RenameColumn(
                name: "AdminPassword",
                table: "CompanyConfigs",
                newName: "OneDriveClientSecret");

            migrationBuilder.AddColumn<string>(
                name: "BlobConnectionString",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BlobContainerName",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FtpHost",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FtpPassword",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FtpUser",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveClientId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveClientSecret",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveRefreshToken",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OneDriveClientId",
                table: "CompanyConfigs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "OneDriveClientId",
                table: "CompanyConfigs");

            migrationBuilder.RenameColumn(
                name: "OneDriveRefreshToken",
                table: "CompanyConfigs",
                newName: "StorageCredentials");

            migrationBuilder.RenameColumn(
                name: "OneDriveClientSecret",
                table: "CompanyConfigs",
                newName: "AdminPassword");

            migrationBuilder.AddColumn<string>(
                name: "AdminUserName",
                table: "CompanyConfigs",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }
    }
}
