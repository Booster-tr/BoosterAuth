using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Licensing.Portal.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTermsAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "TrialRequests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAcceptedBy",
                table: "Orders",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsAcceptedIp",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "Orders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "TrialRequests");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedBy",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedIp",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "Orders");
        }
    }
}
