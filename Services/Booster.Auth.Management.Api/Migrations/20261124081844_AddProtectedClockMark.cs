using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProtectedClockMark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClockMarkProtected",
                table: "Installations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClockMarkProtected",
                table: "Installations");
        }
    }
}
