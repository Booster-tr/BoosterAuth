using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Licensing.Portal.Migrations
{
    /// <inheritdoc />
    public partial class SelfServicePortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_OwnerPrincipalId",
                table: "Customers");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginLicenseId",
                table: "IssuedLicenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransferredToLicenseId",
                table: "IssuedLicenses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerInstallations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Domains = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerInstallations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerInstallations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LicenseTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginLicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromLicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToLicenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToInstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseTransfers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrialRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Domains = table.Column<List<string>>(type: "text[]", nullable: false),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LicenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrialRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OwnerPrincipalId",
                table: "Customers",
                column: "OwnerPrincipalId",
                unique: true,
                filter: "\"OwnerPrincipalId\" IS NOT NULL AND \"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInstallations_CustomerId",
                table: "CustomerInstallations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerInstallations_InstallationId",
                table: "CustomerInstallations",
                column: "InstallationId",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseTransfers_OriginLicenseId_CreatedTime",
                table: "LicenseTransfers",
                columns: new[] { "OriginLicenseId", "CreatedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TrialRequests_CustomerId",
                table: "TrialRequests",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_TrialRequests_IpAddress_CreatedTime",
                table: "TrialRequests",
                columns: new[] { "IpAddress", "CreatedTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerInstallations");

            migrationBuilder.DropTable(
                name: "LicenseTransfers");

            migrationBuilder.DropTable(
                name: "TrialRequests");

            migrationBuilder.DropIndex(
                name: "IX_Customers_OwnerPrincipalId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OriginLicenseId",
                table: "IssuedLicenses");

            migrationBuilder.DropColumn(
                name: "TransferredToLicenseId",
                table: "IssuedLicenses");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OwnerPrincipalId",
                table: "Customers",
                column: "OwnerPrincipalId");
        }
    }
}
