using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Idp.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseUserNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kullanici adlari artik kucuk harfle saklanir ve aranir (IdentifierNormalizer). Eski kayitlar cevrilir;
            // ayni havuzda yalnizca harf buyuklugu farkli ikinci bir hesap varsa (eski hata) o kayitlara dokunulmaz.
            migrationBuilder.Sql("""
                UPDATE "Principals" p
                SET "UserName" = lower(p."UserName")
                WHERE p."UserName" IS NOT NULL
                  AND p."UserName" <> lower(p."UserName")
                  AND NOT EXISTS (
                      SELECT 1 FROM "Principals" q
                      WHERE q."IsolationId" = p."IsolationId"
                        AND q."Id" <> p."Id"
                        AND q."DeletedTime" IS NULL
                        AND lower(q."UserName") = lower(p."UserName"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
