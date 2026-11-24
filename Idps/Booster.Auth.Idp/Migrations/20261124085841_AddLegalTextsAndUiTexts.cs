using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Idp.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalTextsAndUiTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    KepAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RegistryInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UiTexts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiTexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalDocumentTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Customized = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocumentTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegalDocumentTranslations_LegalDocuments_LegalDocumentId",
                        column: x => x.LegalDocumentId,
                        principalTable: "LegalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UiTextTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UiTextId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiTextTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UiTextTranslations_UiTexts_UiTextId",
                        column: x => x.UiTextId,
                        principalTable: "UiTexts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Key",
                table: "LegalDocuments",
                column: "Key",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentTranslations_LegalDocumentId_LanguageCode",
                table: "LegalDocumentTranslations",
                columns: new[] { "LegalDocumentId", "LanguageCode" },
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UiTexts_Key",
                table: "UiTexts",
                column: "Key",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UiTextTranslations_UiTextId_LanguageCode",
                table: "UiTextTranslations",
                columns: new[] { "UiTextId", "LanguageCode" },
                unique: true,
                filter: "\"DeletedTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalDocumentTranslations");

            migrationBuilder.DropTable(
                name: "LegalProfiles");

            migrationBuilder.DropTable(
                name: "UiTextTranslations");

            migrationBuilder.DropTable(
                name: "LegalDocuments");

            migrationBuilder.DropTable(
                name: "UiTexts");
        }
    }
}
