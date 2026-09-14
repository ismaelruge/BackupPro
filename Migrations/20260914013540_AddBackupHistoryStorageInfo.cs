using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupHistoryStorageInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageFileId",
                table: "BackupHistories",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageId",
                table: "BackupHistories",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageType",
                table: "BackupHistories",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StorageFileId",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "StorageId",
                table: "BackupHistories");

            migrationBuilder.DropColumn(
                name: "StorageType",
                table: "BackupHistories");
        }
    }
}
