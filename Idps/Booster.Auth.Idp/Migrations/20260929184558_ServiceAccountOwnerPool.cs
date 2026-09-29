using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Idp.Migrations
{
    /// <inheritdoc />
    public partial class ServiceAccountOwnerPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerPoolId",
                table: "ServiceAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAccounts_OwnerPoolId",
                table: "ServiceAccounts",
                column: "OwnerPoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ServiceAccounts_OwnerPoolId",
                table: "ServiceAccounts");

            migrationBuilder.DropColumn(
                name: "OwnerPoolId",
                table: "ServiceAccounts");
        }
    }
}
