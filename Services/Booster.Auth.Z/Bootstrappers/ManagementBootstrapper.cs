using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Features.Schemas._base;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using OpenFga.Sdk.Model;
using System.Text.Json.Nodes;

namespace Booster.Auth.Z.Bootstrappers;

/// <summary>
/// Booster yönetim store'unu ilk kez kurar. Sonrasında şema arayüzden yönetilir;
/// bootstrapper yayındaki sürümün üzerine yazmaz.
/// </summary>
public sealed class ManagementBootstrapper(
    FgaSchemaWriter schemaWriter,
    FgaRelationManager relationManager,
    IPoolStoreProvider poolStoreProvider,
    IAsyncRepository<PoolSchema, AuthZDbContext> schemaRepository)
{
    private const string SystemSchemaName = "Booster";

    public async Task InitializeAsync(CancellationToken ct)
    {
        var existing = await schemaRepository.GetAsync(
            p => p.PoolId == AuthConstants.ManagementPoolId && p.Status == SchemaVersionStatus.Active,
            cancellationToken: ct);

        if (existing is not null)
        {
            await FillLegacyDefinitionAsync(existing, ct);
            return;
        }

        var definition = SystemSchema.Definition;
        var storeId = await schemaWriter.CreateStoreAsync(AuthConstants.ManagementStoreName, ct);
        var modelId = await schemaWriter.WriteModelAsync(storeId, definition.ToFgaModel(), ct);

        await relationManager.WriteBatchAsync(
            storeId,
            [
                new TupleInput(
                    AuthConstants.User(AuthConstants.AdminUserId),
                    AuthConstants.OwnerRelation,
                    AuthConstants.Tenant(AuthConstants.ManagementTenantId)),

                new TupleInput(
                    AuthConstants.Tenant(AuthConstants.ManagementTenantId),
                    AuthConstants.ParentRelation,
                    AuthConstants.Pool(AuthConstants.ManagementPoolId)),

                new TupleInput(
                    AuthConstants.Pool(AuthConstants.ManagementPoolId),
                    AuthConstants.ParentRelation,
                    AuthConstants.Client(AuthConstants.ManagementClientId))
            ],
            ct);

        var schema = new PoolSchema
        {
            PoolId = AuthConstants.ManagementPoolId,
            StoreId = storeId,
            Name = SystemSchemaName,
            Status = SchemaVersionStatus.Active,
            Definition = definition,
            ModelId = modelId,
            Revision = 1,
            ActivatedTime = DateTimeOffset.UtcNow
        };
        schema.SetId();

        await schemaRepository.AddAsync(schema, ct);
        await schemaRepository.SaveChangesAsync(ct);

        await poolStoreProvider.RemoveCacheAsync(AuthConstants.ManagementPoolId, ct);
    }

    /// <summary>
    /// Arayüz öncesinden kalan sürümün tanımı boştur (model yalnızca koddaydı); bir kez sistem tanımıyla doldurulur.
    /// Model aynıysa OpenFGA'ya yazılmaz.
    /// </summary>
    private async Task FillLegacyDefinitionAsync(PoolSchema schema, CancellationToken ct)
    {
        if (schema.Definition.Types.Count > 0)
            return;

        var definition = SystemSchema.Definition;
        var desired = definition.ToFgaModel();
        var active = schema.ModelId is null ? null : await schemaWriter.GetModelAsync(schema.StoreId, schema.ModelId, ct);

        if (active is null || Fingerprint(active.TypeDefinitions) != Fingerprint(desired.TypeDefinitions))
            schema.ModelId = await schemaWriter.WriteModelAsync(schema.StoreId, desired, ct);

        schema.Definition = definition;
        schema.Revision++;

        await schemaRepository.SaveChangesAsync(ct);
        await poolStoreProvider.RemoveCacheAsync(AuthConstants.ManagementPoolId, ct);
    }

    private static string Fingerprint(IEnumerable<TypeDefinition> types)
        => string.Join(';', types
            .OrderBy(t => t.Type, StringComparer.Ordinal)
            .SelectMany(t => (t.Relations ?? []).Keys
                .Order(StringComparer.Ordinal)
                .Select(relation =>
                {
                    var assignable = (t.Metadata?.Relations?.GetValueOrDefault(relation)?.DirectlyRelatedUserTypes ?? [])
                        .Select(u => $"{u.Type}#{u.Relation}")
                        .Order(StringComparer.Ordinal);

                    var userset = Normalize(JsonNode.Parse(t.Relations![relation].ToJson()))?.ToJsonString();

                    return $"{t.Type}.{relation}={userset}|{string.Join(',', assignable)}";
                })));

    private static JsonNode? Normalize(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(p => p.Key).ToList())
            {
                var value = obj[key];

                if (value is null || (value is JsonValue v && v.TryGetValue<string>(out var text) && text.Length == 0))
                    obj.Remove(key);
                else
                    Normalize(value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) Normalize(item);
        }

        return node;
    }
}
