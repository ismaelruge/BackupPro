using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DatabaseSources");

            migrationBuilder.CreateTable(
                name: "MongoDBDataBases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AuthenticationDatabase = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SslEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReplicaSet = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MongoDBDataBases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MySqlDataBases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SslMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    Charset = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MySqlDataBases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PostgresSqlDataBases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    SslMode = table.Column<bool>(type: "INTEGER", nullable: false),
                    SearchPath = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostgresSqlDataBases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SqlServerDataBases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConfigurationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Host = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IntegratedSecurity = table.Column<bool>(type: "INTEGER", nullable: false),
                    TrustServerCertificate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SqlServerDataBases", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MongoDBDataBases");

            migrationBuilder.DropTable(
                name: "MySqlDataBases");

            migrationBuilder.DropTable(
                name: "PostgresSqlDataBases");

            migrationBuilder.DropTable(
                name: "SqlServerDataBases");

            migrationBuilder.CreateTable(
                name: "DatabaseSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackupName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DatabaseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Server = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UseWindowsAuth = table.Column<bool>(type: "INTEGER", nullable: false),
                    User = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DatabaseSources", x => x.Id);
                });
        }
    }
}
