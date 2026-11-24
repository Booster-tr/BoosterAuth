using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Management.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfDeletionPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kayit politikasina "Hesabim'dan hesap silme" alani (jsonb). Mevcut havuzlarda acikca KAPALI yazilir.
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = jsonb_set("Policy", '{Registration,AllowSelfDeletion}', 'false'::jsonb, true)
                WHERE "Policy" ? 'Registration' AND NOT ("Policy"->'Registration' ? 'AllowSelfDeletion');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPools"
                SET "Policy" = "Policy" #- '{Registration,AllowSelfDeletion}'
                WHERE "Policy" ? 'Registration';
                """);
        }
    }
}
