using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class TenantOwnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mevcut firmalar admin tarafından açıldı; sahipleri admin olarak
            // doldurulur. Varsayılan değer yalnızca bu doldurma içindir ve
            // hemen kaldırılır: yeni kayıtta sahip her zaman açıkça yazılmalı.
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Tenants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.Sql("ALTER TABLE \"Tenants\" ALTER COLUMN \"OwnerId\" DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OwnerId",
                table: "Tenants",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_OwnerId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Tenants");
        }
    }
}
