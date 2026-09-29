using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Features._base.Entities;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Builders;
using Booster.Authorization.Relational.Fga;
using Booster.Database.Repositories;
using OpenFga.Sdk.Client.Model;

namespace Booster.Auth.Z.Bootstrappers;

public sealed class ManagementBootstrapper(
    FgaSchemaWriter schemaWriter,
    FgaRelationManager relationManager,
    IPoolStoreProvider poolStoreProvider,
    IAsyncRepository<PoolStore, AuthZDbContext> poolStoreRepository)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var existing = await poolStoreProvider.GetByPoolIdAsync(AuthConstants.ManagementPoolId, ct);
        if (existing is not null)
        {
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

    private static ClientWriteAuthorizationModelRequest BuildManagementSchema()
        => new FgaSchemaBuilder()
            .Type(AuthConstants.TenantType, t => t
                .Relation(AuthConstants.OwnerRelation, AuthConstants.UserPrefix)
                .Relation(AuthConstants.ManagerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.OwnerRelation))
                .Relation(AuthConstants.ViewerRelation, r => r
                    .Assignable(AuthConstants.UserPrefix)
                    .Includes(AuthConstants.ManagerRelation)))
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
                    .InheritsFrom(AuthConstants.ViewerRelation, AuthConstants.ParentRelation)))
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
                    .InheritsFrom(AuthConstants.ViewerRelation, AuthConstants.ParentRelation)))
            .Build();
}
