using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Z.Migrations
{
    /// <inheritdoc />
    public partial class InitialAuthZ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PoolStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActiveModelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolStores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PoolStores_PoolId",
                table: "PoolStores",
                column: "PoolId",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PoolStores");
        }
    }
}
