using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class LoginPolicyDefaultCountryCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Var olan havuzlara varsayilan ulke kodu yazilir.
            //
            // Yazilmasaydi alan null kalir ve telefonla giriste ulke kodu
            // yazmayan kullanici REDDEDILIRDI (bkz. PhoneCodeProvider) —
            // sebebi de gorunmezdi, cunku sema bozulmuyor.
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = jsonb_set("Policy"::jsonb, '{Login,DefaultCountryCode}', '"90"')::text::jsonb
                WHERE "Policy"::jsonb #> '{Login,DefaultCountryCode}' IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = ("Policy"::jsonb #- '{Login,DefaultCountryCode}')::text::jsonb;
                """);
        }
    }
}
