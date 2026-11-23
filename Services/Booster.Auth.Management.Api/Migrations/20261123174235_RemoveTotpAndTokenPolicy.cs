using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTotpAndTokenPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TOTP kaldirildi: havuz MFA yontemlerinden TOTP bitini (1) cikar. Yalnizca TOTP kalan havuza e-posta kodu (4)
            // verilir; aksi halde "MFA zorunlu" bir havuz yontemsiz kalirdi (eskiden bu durumda MFA hic uygulanmiyordu).
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = jsonb_set("Policy", '{Mfa,AllowedMethods}',
                    to_jsonb(CASE WHEN (("Policy"->'Mfa'->>'AllowedMethods')::int & ~1) = 0 THEN 4
                                  ELSE (("Policy"->'Mfa'->>'AllowedMethods')::int & ~1) END))
                WHERE "Policy" ? 'Mfa' AND (("Policy"->'Mfa'->>'AllowedMethods')::int & 1) = 1;
                """);

            migrationBuilder.DropColumn(
                name: "TokenScopePolicy",
                table: "Clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenScopePolicy",
                table: "Clients",
                type: "jsonb",
                nullable: true);
        }
    }
}
