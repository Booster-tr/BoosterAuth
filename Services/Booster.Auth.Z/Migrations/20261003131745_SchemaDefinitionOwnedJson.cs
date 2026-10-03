using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Z.Migrations
{
    /// <inheritdoc />
    public partial class SchemaDefinitionOwnedJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tanım owned JSON oldu; EF özellik adlarını (PascalCase) kullanır. Önceki camelCase anahtarlar dönüştürülür.
            // Desen yalnızca anahtarları yakalar: önünde ters bölü olan (metin içindeki) tırnaklar atlanır.
            migrationBuilder.Sql("""
                UPDATE "PoolSchemas" SET "Definition" = (regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace("Definition"::text, '(^|[^\\])"types":', '\1"Types":', 'g'), '(^|[^\\])"name":', '\1"Name":', 'g'), '(^|[^\\])"displayName":', '\1"DisplayName":', 'g'), '(^|[^\\])"description":', '\1"Description":', 'g'), '(^|[^\\])"x":', '\1"X":', 'g'), '(^|[^\\])"y":', '\1"Y":', 'g'), '(^|[^\\])"relations":', '\1"Relations":', 'g'), '(^|[^\\])"assignableTypes":', '\1"AssignableTypes":', 'g'), '(^|[^\\])"includes":', '\1"Includes":', 'g'), '(^|[^\\])"inherits":', '\1"Inherits":', 'g'), '(^|[^\\])"via":', '\1"Via":', 'g'), '(^|[^\\])"relation":', '\1"Relation":', 'g'))::jsonb;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "PoolSchemas" SET "Definition" = (regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace(regexp_replace("Definition"::text, '(^|[^\\])"Types":', '\1"types":', 'g'), '(^|[^\\])"Name":', '\1"name":', 'g'), '(^|[^\\])"DisplayName":', '\1"displayName":', 'g'), '(^|[^\\])"Description":', '\1"description":', 'g'), '(^|[^\\])"X":', '\1"x":', 'g'), '(^|[^\\])"Y":', '\1"y":', 'g'), '(^|[^\\])"Relations":', '\1"relations":', 'g'), '(^|[^\\])"AssignableTypes":', '\1"assignableTypes":', 'g'), '(^|[^\\])"Includes":', '\1"includes":', 'g'), '(^|[^\\])"Inherits":', '\1"inherits":', 'g'), '(^|[^\\])"Via":', '\1"via":', 'g'), '(^|[^\\])"Relation":', '\1"relation":', 'g'))::jsonb;
                """);
        }
    }
}
