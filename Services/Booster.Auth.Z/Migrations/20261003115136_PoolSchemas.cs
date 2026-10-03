using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Z.Migrations
{
    /// <inheritdoc />
    public partial class PoolSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoolSchemas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    PublishedVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolSchemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoolSchemaVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolSchemaVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemas_PoolId",
                table: "PoolSchemas",
                column: "PoolId",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemaVersions_PoolId_Version",
                table: "PoolSchemaVersions",
                columns: new[] { "PoolId", "Version" },
                unique: true,
                filter: "\"DeletedTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PoolSchemas");

            migrationBuilder.DropTable(
                name: "PoolSchemaVersions");
        }
    }
}
