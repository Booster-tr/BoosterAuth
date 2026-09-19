using Booster.Auth.Z.Managers.LookupManagers;
using Booster.Auth.Z.Managers.LookupManagers.Models;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Booster.Auth.Z.Grpc;

[Authorize]
public sealed class AuthorizationGrpcService(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager,
    ILookupManager lookupManager) : AuthorizationService.AuthorizationServiceBase
{
    public override async Task<CheckResponse> Check(CheckRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        var allowed = await relationManager.CheckAsync(
            storeId,
            request.User,
            request.Relation,
            request.Object,
            modelId,
            context.CancellationToken);

        return new CheckResponse { Allowed = allowed };
    }

    public override async Task<BatchCheckResponse> BatchCheck(BatchCheckRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        var checks = request.Checks
            .Select(c => new CheckInput(c.User, c.Relation, c.Object, c.CorrelationId))
            .ToList();

        var results = await relationManager.BatchCheckAsync(storeId, checks, modelId, context.CancellationToken);

        var response = new BatchCheckResponse();
        response.Results.AddRange(results.Select(r => new BatchCheckResultItem
        {
            CorrelationId = r.CorrelationId,
            Allowed = r.Allowed
        }));

        return response;
    }

    public override async Task<LookupResponse> LookupByUser(
        LookupByUserRequest request, ServerCallContext context)
    {
        var results = await lookupManager.LookupByUserAsync(
            ParsePoolId(request.IsolationId),
            request.User,
            request.ObjectType,
            NullIfEmpty(request.Relation),
            context.CancellationToken);

        return ToLookupResponse(results);
    }

    public override async Task<LookupResponse> LookupByObject(
        LookupByObjectRequest request, ServerCallContext context)
    {
        var results = await lookupManager.LookupByObjectAsync(
            ParsePoolId(request.IsolationId),
            request.Object,
            NullIfEmpty(request.Relation),
            context.CancellationToken);

        return ToLookupResponse(results);
    }

    private static LookupResponse ToLookupResponse(IReadOnlyList<LookupTuple> results)
    {
        var response = new LookupResponse();
        response.Results.AddRange(results.Select(r => new LookupResultItem
        {
            User = r.User,
            Relation = r.Relation,
            Object = r.Object
        }));

        return response;
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static Guid ParsePoolId(string isolationId)
        => Guid.TryParse(isolationId, out var poolId) && poolId != Guid.Empty
            ? poolId
            : throw new RpcException(new Status(
                StatusCode.InvalidArgument, "isolation_id geçerli bir Guid olmalıdır."));

    private async Task<(string StoreId, string ModelId)> ResolveStoreAsync(string isolationId, CancellationToken ct)
    {
        if (!Guid.TryParse(isolationId, out var poolId) || poolId == Guid.Empty)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "isolation_id geçerli bir Guid olmalıdır."));

        var mapping = await poolStoreProvider.GetByPoolIdAsync(poolId, ct)
            ?? throw new RpcException(new Status(
                StatusCode.FailedPrecondition, $"'{poolId}' pool'u için store eşlemesi bulunamadı."));

        return (mapping.StoreId, mapping.ActiveModelId);
    }
}
