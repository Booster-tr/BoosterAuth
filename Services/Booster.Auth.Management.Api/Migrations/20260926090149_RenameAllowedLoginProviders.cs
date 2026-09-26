using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <summary>
    /// <c>Policy.Login.AllowedCredentialProviders</c> anahtarını
    /// <c>AllowedLoginProviders</c> olarak yeniden adlandırır.
    /// <para>
    /// Politika JSON kolonunda tutuluyor ve anahtar adı CLR özellik adından
    /// türüyor. Bu güncelleme olmadan var olan havuzlar yeni adı bulamaz, alan
    /// varsayılan değere (<c>None</c>) düşer ve o havuzdaki HİÇBİR kullanıcı
    /// giriş yapamaz — şema bozulmadığı için hata da alınmaz.
    /// </para>
    /// </summary>
    public partial class RenameAllowedLoginProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = jsonb_set(
                        "Policy"::jsonb #- '{Login,AllowedCredentialProviders}',
                        '{Login,AllowedLoginProviders}',
                        "Policy"::jsonb #> '{Login,AllowedCredentialProviders}')::text::jsonb
                WHERE "Policy"::jsonb #> '{Login,AllowedCredentialProviders}' IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = jsonb_set(
                        "Policy"::jsonb #- '{Login,AllowedLoginProviders}',
                        '{Login,AllowedCredentialProviders}',
                        "Policy"::jsonb #> '{Login,AllowedLoginProviders}')::text::jsonb
                WHERE "Policy"::jsonb #> '{Login,AllowedLoginProviders}' IS NOT NULL;
                """);
        }
    }
}
