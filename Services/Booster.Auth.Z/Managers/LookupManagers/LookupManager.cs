using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Z.Managers.LookupManagers.Models;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Auth.Z.Stores.PoolStores.Models;
using Booster.Authorization.Relational.Fga;
using Booster.Authorization.Relational.Models;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Z.Managers.LookupManagers;

public sealed class LookupManager(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager,
    FgaSchemaWriter schemaWriter,
    FgaSchemaParser schemaParser,
    ICacheService cacheService) : ILookupManager
{
    private static readonly TimeSpan SchemaTtl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<LookupTuple>> LookupByUserAsync(
        Guid poolId, string user, string objectType, string? relation, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(poolId, ct);
        var relations = await ResolveRelationsAsync(mapping, objectType, relation, ct);

        var results = new List<LookupTuple>();

        foreach (var current in relations)
        {
            var objects = await relationManager.ListObjectsAsync(
                mapping.StoreId, user, current, objectType, mapping.ActiveModelId, ct);

            results.AddRange(objects.Select(o => new LookupTuple(user, current, o)));
        }

        return results;
    }

    public async Task<IReadOnlyList<LookupTuple>> LookupByObjectAsync(
        Guid poolId, string @object, string? relation, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(poolId, ct);

        var objectType = ObjectTypeOf(@object);
        var relations = await ResolveRelationsAsync(mapping, objectType, relation, ct);

        var results = new List<LookupTuple>();

        foreach (var current in relations)
        {
            var users = await relationManager.ListUsersAsync(
                mapping.StoreId, @object, current, AuthConstants.UserPrefix, mapping.ActiveModelId, ct);

            results.AddRange(users.Select(u => new LookupTuple(u, current, @object)));
        }

        return results;
    }

    public async Task<IReadOnlyList<ObjectAccess>> LookupMyAccessAsync(
        Guid poolId, string user, IReadOnlyList<string> objectTypes, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(poolId, ct);

        var byObject = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var objectType in objectTypes.Distinct(StringComparer.Ordinal))
        {
            var relations = await ResolveRelationsAsync(mapping, objectType, relation: null, ct);

            foreach (var current in relations)
            {
                var objects = await relationManager.ListObjectsAsync(
                    mapping.StoreId, user, current, objectType, mapping.ActiveModelId, ct);

                foreach (var @object in objects)
                {
                    if (!byObject.TryGetValue(@object, out var list))
                        byObject[@object] = list = [];

                    list.Add(current);
                }
            }
        }

        return byObject
            .Select(pair => ToAccess(pair.Key, pair.Value))
            .ToList();
    }

    public async Task<ObjectAccess> LookupObjectAccessAsync(
        Guid poolId, string user, string @object, CancellationToken ct)
    {
        var mapping = await poolStoreProvider.GetRequiredByPoolIdAsync(poolId, ct);

        var objectType = ObjectTypeOf(@object);
        var relations = await ResolveRelationsAsync(mapping, objectType, relation: null, ct);

        var checks = relations
            .Select((current, index) => new CheckInput(user, current, @object, index.ToString()))
            .ToList();

        var results = await relationManager.BatchCheckAsync(
            mapping.StoreId, checks, mapping.ActiveModelId, ct);

        var allowed = results
            .Where(r => r.Allowed)
            .Select(r => relations[int.Parse(r.CorrelationId)])
            .ToList();

        return ToAccess(@object, allowed);
    }

    private static ObjectAccess ToAccess(string @object, List<string> relations)
    {
        var separator = @object.IndexOf(':');

        return new ObjectAccess(
            @object[..separator],
            @object[(separator + 1)..],
            relations.Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    private async Task<IReadOnlyList<string>> ResolveRelationsAsync(
        PoolStoreData mapping, string objectType, string? relation, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(relation))
            return [relation];

        var types = await GetSchemaAsync(mapping, ct);

        var type = types.FirstOrDefault(t => t.Name == objectType)
            ?? throw new BusinessException(AuthZMessageKeys.ResourceTypeNotFound);

        return type.Relations
            .Where(r => r.DirectlyAssignable.Contains(AuthConstants.UserPrefix) || r.Rules.Count > 0)
            .Select(r => r.Name)
            .ToList();
    }

    private async Task<IReadOnlyList<ResourceType>> GetSchemaAsync(PoolStoreData mapping, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(mapping.ActiveModelId))
            throw new BusinessException(AuthZMessageKeys.SchemaNotFound);

        return await cacheService.GetOrSetAsync<IReadOnlyList<ResourceType>>(
            $"authz:schema:{mapping.StoreId}:{mapping.ActiveModelId}",
            async token =>
            {
                var model = await schemaWriter.GetModelAsync(mapping.StoreId, mapping.ActiveModelId, token)
                    ?? throw new BusinessException(AuthZMessageKeys.SchemaNotFound);

                return schemaParser.Parse(model);
            },
            expiry: SchemaTtl,
            ct: ct);
    }

    private static string ObjectTypeOf(string @object)
    {
        var separator = @object.IndexOf(':');

        if (separator <= 0 || separator == @object.Length - 1)
            throw new BusinessException(AuthZMessageKeys.InvalidObjectFormat);

        return @object[..separator];
    }
}
