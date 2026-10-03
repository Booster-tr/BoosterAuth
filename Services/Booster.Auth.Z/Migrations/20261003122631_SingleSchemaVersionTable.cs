using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Booster.Auth.Z.Migrations
{
    /// <inheritdoc />
    public partial class SingleSchemaVersionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Taslak satırları tek havuz tek satır idi; artık havuz başına çok sürüm olacak.
            migrationBuilder.DropIndex(name: "IX_PoolSchemas_PoolId", table: "PoolSchemas");

            migrationBuilder.AddColumn<string>(name: "StoreId", table: "PoolSchemas", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<string>(name: "Name", table: "PoolSchemas", type: "character varying(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<int>(name: "Status", table: "PoolSchemas", type: "integer", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ModelId", table: "PoolSchemas", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "ActivatedTime", table: "PoolSchemas", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<Guid>(name: "ActivatedBy", table: "PoolSchemas", type: "uuid", nullable: true);

            // Veri taşıma: yayınlanmış sürümler adlandırılmış satırlara dönüşür; yayındaki aktif, diğerleri pasif olur.
            migrationBuilder.Sql("""
                INSERT INTO "PoolSchemas" ("Id", "PoolId", "StoreId", "Name", "Status", "Definition", "ModelId", "Revision",
                                           "ActivatedTime", "ActivatedBy", "CreatedTime", "PublishedVersion")
                SELECT gen_random_uuid(), v."PoolId", ps."StoreId", 'Sürüm ' || v."Version",
                       CASE WHEN v."Version" = d."PublishedVersion" THEN 2 ELSE 3 END,
                       v."Definition", v."ModelId", 1, v."CreatedTime", v."PublishedBy", v."CreatedTime", 0
                FROM "PoolSchemaVersions" v
                JOIN "PoolSchemas" d ON d."PoolId" = v."PoolId" AND d."DeletedTime" IS NULL AND d."Name" IS NULL
                JOIN "PoolStores" ps ON ps."PoolId" = v."PoolId" AND ps."DeletedTime" IS NULL
                WHERE v."DeletedTime" IS NULL;
                """);

            // Eski taslak: yayındakinden farklıysa "Taslak" sürümü olarak kalır, aynıysa silinir.
            migrationBuilder.Sql("""
                DELETE FROM "PoolSchemas" d
                USING "PoolSchemaVersions" v
                WHERE d."Name" IS NULL AND v."PoolId" = d."PoolId" AND v."Version" = d."PublishedVersion"
                  AND v."Definition" = d."Definition";

                UPDATE "PoolSchemas" d
                SET "Name" = 'Taslak', "Status" = 1, "StoreId" = ps."StoreId"
                FROM "PoolStores" ps
                WHERE d."Name" IS NULL AND ps."PoolId" = d."PoolId" AND ps."DeletedTime" IS NULL;
                """);

            // Şeması olmayan store eşlemeleri (Booster yönetim havuzu) aktif sürüm olur; içindeki yetki kayıtları korunur.
            migrationBuilder.Sql("""
                INSERT INTO "PoolSchemas" ("Id", "PoolId", "StoreId", "Name", "Status", "Definition", "ModelId", "Revision",
                                           "ActivatedTime", "CreatedTime", "PublishedVersion")
                SELECT gen_random_uuid(), ps."PoolId", ps."StoreId",
                       CASE WHEN ps."PoolId" = '00000000-0000-0000-0000-000000000001' THEN 'Booster' ELSE 'Sürüm 1' END,
                       2, '{"types":[]}', NULLIF(ps."ActiveModelId", ''), 1, ps."CreatedTime", ps."CreatedTime", 0
                FROM "PoolStores" ps
                WHERE ps."DeletedTime" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM "PoolSchemas" s WHERE s."PoolId" = ps."PoolId" AND s."Status" = 2);
                """);

            migrationBuilder.Sql("""DELETE FROM "PoolSchemas" WHERE "Name" IS NULL;""");

            migrationBuilder.AlterColumn<string>(name: "StoreId", table: "PoolSchemas", type: "character varying(50)", maxLength: 50, nullable: false, oldClrType: typeof(string), oldType: "character varying(50)", oldMaxLength: 50, oldNullable: true);
            migrationBuilder.AlterColumn<string>(name: "Name", table: "PoolSchemas", type: "character varying(100)", maxLength: 100, nullable: false, oldClrType: typeof(string), oldType: "character varying(100)", oldMaxLength: 100, oldNullable: true);
            migrationBuilder.AlterColumn<int>(name: "Status", table: "PoolSchemas", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.DropColumn(name: "PublishedVersion", table: "PoolSchemas");
            migrationBuilder.DropTable(name: "PoolSchemaVersions");
            migrationBuilder.DropTable(name: "PoolStores");

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemas_PoolId_Active",
                table: "PoolSchemas",
                column: "PoolId",
                unique: true,
                filter: "\"Status\" = 2 AND \"DeletedTime\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemas_PoolId_Name",
                table: "PoolSchemas",
                columns: new[] { "PoolId", "Name" },
                unique: true,
                filter: "\"DeletedTime\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri dönüşte yalnızca store eşlemesi aktif sürümden yeniden kurulur; sürüm geçmişi geri gelmez.
            migrationBuilder.CreateTable(
                name: "PoolStores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActiveModelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_PoolStores", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PoolStores_PoolId",
                table: "PoolStores",
                column: "PoolId",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.Sql("""
                INSERT INTO "PoolStores" ("Id", "PoolId", "StoreId", "ActiveModelId", "CreatedTime")
                SELECT gen_random_uuid(), "PoolId", "StoreId", COALESCE("ModelId", ''), "CreatedTime"
                FROM "PoolSchemas" WHERE "Status" = 2 AND "DeletedTime" IS NULL;
                DELETE FROM "PoolSchemas";
                """);

            migrationBuilder.CreateTable(
                name: "PoolSchemaVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_PoolSchemaVersions", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemaVersions_PoolId_Version",
                table: "PoolSchemaVersions",
                columns: new[] { "PoolId", "Version" },
                unique: true,
                filter: "\"DeletedTime\" IS NULL");

            migrationBuilder.DropIndex(name: "IX_PoolSchemas_PoolId_Active", table: "PoolSchemas");
            migrationBuilder.DropIndex(name: "IX_PoolSchemas_PoolId_Name", table: "PoolSchemas");

            migrationBuilder.DropColumn(name: "ActivatedBy", table: "PoolSchemas");
            migrationBuilder.DropColumn(name: "ActivatedTime", table: "PoolSchemas");
            migrationBuilder.DropColumn(name: "ModelId", table: "PoolSchemas");
            migrationBuilder.DropColumn(name: "Name", table: "PoolSchemas");
            migrationBuilder.DropColumn(name: "Status", table: "PoolSchemas");
            migrationBuilder.DropColumn(name: "StoreId", table: "PoolSchemas");

            migrationBuilder.AddColumn<int>(name: "PublishedVersion", table: "PoolSchemas", type: "integer", nullable: false, defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PoolSchemas_PoolId",
                table: "PoolSchemas",
                column: "PoolId",
                unique: true,
                filter: "\"DeletedTime\" IS NULL");
        }
    }
}
