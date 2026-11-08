using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClientLogoUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUri",
                table: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUri",
                table: "Clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
