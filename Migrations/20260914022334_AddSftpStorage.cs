using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class AddSftpStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SftpStorages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RemotePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SftpStorages", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SftpStorages");
        }
    }
}
