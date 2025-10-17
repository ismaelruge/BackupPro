using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BackupPro.Migrations
{
    /// <inheritdoc />
    public partial class AddMongoDBDataBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthenticationDatabase",
                table: "MongoDBDataBases");

            migrationBuilder.DropColumn(
                name: "ReplicaSet",
                table: "MongoDBDataBases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthenticationDatabase",
                table: "MongoDBDataBases",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplicaSet",
                table: "MongoDBDataBases",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }
    }
}
