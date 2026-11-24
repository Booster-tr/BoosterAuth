using Booster.Auth.Z.Managers.LookupManagers;
using Booster.Auth.Z.Managers.LookupManagers.Models;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Authorization.Relational.Fga;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using OpenFga.Sdk.Exceptions;

namespace Booster.Auth.Z.Grpc;

[AllowAnonymous]
public sealed class AuthorizationGrpcService(
    IPoolStoreProvider poolStoreProvider,
    FgaRelationManager relationManager,
    FgaSchemaWriter schemaWriter,
    ILookupManager lookupManager) : AuthorizationService.AuthorizationServiceBase
{
    public override async Task<CheckResponse> Check(CheckRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        var allowed = await RejectInvalidAsync(() => relationManager.CheckAsync(
            storeId,
            request.User,
            request.Relation,
            request.Object,
            modelId,
            context.CancellationToken));

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

    public override async Task<LookupResponse> ReadRelations(
        ReadRelationsRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        // Yalnizca kullaniciyla okuma: OpenFGA nesne tipi ister ("object type field is required"). Modeldeki her tip
        // icin ayri okunur ve TEK yanitta doner (sayfalama yok). Kullanici silinirken tum iliskilerini bulmak icindir;
        // once hic okunamiyor, silinen kullanicinin iliskileri kaliyordu.
        if (!string.IsNullOrEmpty(request.User) && string.IsNullOrEmpty(request.Object))
            return await ReadAllForUserAsync(storeId, modelId, request, context.CancellationToken);

        var page = await relationManager.ReadPageAsync(
            storeId,
            NullIfEmpty(request.User),
            NullIfEmpty(request.Relation),
            NullIfEmpty(request.Object),
            request.PageSize,
            NullIfEmpty(request.ContinuationToken),
            context.CancellationToken);

        var response = new LookupResponse { ContinuationToken = page.ContinuationToken ?? string.Empty };
        response.Results.AddRange(page.Tuples.Select(t => new LookupResultItem
        {
            User = t.User,
            Relation = t.Relation,
            Object = t.Object
        }));

        return response;
    }

    public override async Task<WriteRelationsResponse> WriteRelations(
        WriteRelationsRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        var tuples = ToTuples(request);

        await RejectInvalidAsync(() => relationManager.WriteBatchAsync(storeId, tuples, modelId, context.CancellationToken));

        return new WriteRelationsResponse { AffectedCount = tuples.Count };
    }

    public override async Task<WriteRelationsResponse> DeleteRelations(
        WriteRelationsRequest request, ServerCallContext context)
    {
        var (storeId, modelId) = await ResolveStoreAsync(request.IsolationId, context.CancellationToken);

        var tuples = ToTuples(request);

        await RejectInvalidAsync(() => relationManager.DeleteBatchAsync(storeId, tuples, modelId, context.CancellationToken));

        return new WriteRelationsResponse { AffectedCount = tuples.Count };
    }

    /// <summary>Semaya uymayan tuple / tip: Unknown yerine InvalidArgument (cagiran 400'e cevirebilsin).</summary>
    private static async Task<T> RejectInvalidAsync<T>(Func<Task<T>> call)
    {
        try { return await call(); }
        catch (FgaApiValidationError ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
    }

    private static async Task RejectInvalidAsync(Func<Task> call)
    {
        try { await call(); }
        catch (FgaApiValidationError ex) { throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
    }

    private static List<TupleInput> ToTuples(WriteRelationsRequest request)
    {
        if (request.Tuples.Count == 0)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument, "En az bir tuple gönderilmelidir."));

        return request.Tuples
            .Select(t => new TupleInput(t.User, t.Relation, t.Object))
            .ToList();
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

    private async Task<LookupResponse> ReadAllForUserAsync(
        string storeId, string modelId, ReadRelationsRequest request, CancellationToken ct)
    {
        var model = await schemaWriter.GetModelAsync(storeId, modelId, ct);
        var response = new LookupResponse();

        foreach (var type in model?.TypeDefinitions.Select(t => t.Type) ?? [])
        {
            string? continuation = null;
            do
            {
                var page = await relationManager.ReadPageAsync(
                    storeId, request.User, NullIfEmpty(request.Relation), $"{type}:", 100, continuation, ct);

                response.Results.AddRange(page.Tuples.Select(t => new LookupResultItem
                {
                    User = t.User,
                    Relation = t.Relation,
                    Object = t.Object
                }));
                continuation = page.ContinuationToken;
            } while (!string.IsNullOrEmpty(continuation));
        }

        return response;
    }

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
