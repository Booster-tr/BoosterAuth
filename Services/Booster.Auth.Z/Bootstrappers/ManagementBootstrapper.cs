using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Builders;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;
using System.Text.Json.Nodes;

namespace Booster.Auth.Z.Bootstrappers;

public sealed class ManagementBootstrapper(
    FgaSchemaWriter schemaWriter,
    FgaRelationManager relationManager,
    IPoolStoreProvider poolStoreProvider,
    IAsyncRepository<PoolStore, AuthZDbContext> poolStoreRepository)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var existing = await poolStoreRepository.GetAsync(
            p => p.PoolId == AuthConstants.ManagementPoolId, cancellationToken: ct);

        if (existing is not null)
        {
            await SyncSchemaAsync(existing, ct);
            return;
        }

        var storeId = await schemaWriter.CreateStoreAsync(AuthConstants.ManagementStoreName, ct);

        var modelId = await schemaWriter.WriteModelAsync(storeId, BuildManagementSchema(), ct);

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

        var mapping = new PoolStore
        {
            PoolId = AuthConstants.ManagementPoolId,
            StoreId = storeId,
            ActiveModelId = modelId,
            CreatedTime = DateTimeOffset.UtcNow
        };
        mapping.SetId();

        await poolStoreRepository.AddAsync(mapping, ct);
        await poolStoreRepository.SaveChangesAsync(ct);

        await poolStoreProvider.RemoveCacheAsync(AuthConstants.ManagementPoolId, ct);
    }

    private async Task SyncSchemaAsync(PoolStore mapping, CancellationToken ct)
    {
        var desired = BuildManagementSchema();
        var active = await schemaWriter.GetModelAsync(mapping.StoreId, mapping.ActiveModelId, ct);

        if (active is not null && Fingerprint(active.TypeDefinitions) == Fingerprint(desired.TypeDefinitions))
            return;

        mapping.ActiveModelId = await schemaWriter.WriteModelAsync(mapping.StoreId, desired, ct);
        await poolStoreRepository.SaveChangesAsync(ct);

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

    private static ClientWriteAuthorizationModelRequest BuildManagementSchema()
        => new FgaSchemaBuilder()
            .Type(AuthConstants.TenantType, t => t
                .Relation(AuthConstants.OwnerRelation, AuthConstants.UserPrefix)
                .Relation(AuthConstants.ManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation))
                .Relation(AuthConstants.ViewerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.ManagerRelation)
                    .Includes(AuthConstants.GrantManagerRelation))
                .Relation(AuthConstants.GrantManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation)))
            .Type(AuthConstants.PoolType, t => t
                .Relation(AuthConstants.ParentRelation, AuthConstants.TenantType)
                .Relation(AuthConstants.EndUserRelation, AuthConstants.UserPrefix)
                .Relation(AuthConstants.OwnerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .InheritsFrom(AuthConstants.OwnerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.ManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation)
                    .InheritsFrom(AuthConstants.ManagerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.ViewerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.ManagerRelation)
                    .Includes(AuthConstants.GrantManagerRelation)
                    .InheritsFrom(AuthConstants.ViewerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.GrantManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation)
                    .InheritsFrom(AuthConstants.GrantManagerRelation, AuthConstants.ParentRelation)))
            .Type(AuthConstants.ClientType, t => t
                .Relation(AuthConstants.ParentRelation, AuthConstants.PoolType)
                .Relation(AuthConstants.OwnerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .InheritsFrom(AuthConstants.OwnerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.ManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation)
                    .InheritsFrom(AuthConstants.ManagerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.ViewerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.ManagerRelation)
                    .Includes(AuthConstants.GrantManagerRelation)
                    .InheritsFrom(AuthConstants.ViewerRelation, AuthConstants.ParentRelation))
                .Relation(AuthConstants.GrantManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation)
                    .InheritsFrom(AuthConstants.GrantManagerRelation, AuthConstants.ParentRelation)))
            .Build();
}
