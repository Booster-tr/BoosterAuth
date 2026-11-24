using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Model;

namespace Booster.Authorization.Relational.Fga;

public sealed class FgaRelationManager(IFgaClientProvider clientProvider)
{
    /// <summary>
    /// Yazma / silme idempotenttir: var olan iliskiyi yazmak ya da olmayani silmek hata degildir (OpenFGA
    /// on_duplicate / on_missing = ignore). Yeniden denenen istekler (ag hatasi, cift tiklama) ayni sonucu verir.
    /// </summary>
    // modelId: yazma havuzun CANLI modeline gore dogrulanir. Verilmezse OpenFGA depodaki EN SON modeli kullanir; eski bir
    // surum yeniden canliya alindiginda yazmalar yanlis modele (or. bos taslak) gore reddediliyordu.
    private static readonly ClientWriteOptions Idempotent = new()
    {
        Conflict = new ConflictOptions
        {
            OnDuplicateWrites = OnDuplicateWrites.Ignore,
            OnMissingDeletes = OnMissingDeletes.Ignore
        }
    };

    public async Task WriteAsync(
        string storeId, string user, string relation, string @object, string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Writes = [new ClientTupleKey { User = user, Relation = relation, Object = @object }]
        }, Idempotent, cancellationToken: ct);
    }

    public async Task WriteBatchAsync(
        string storeId, IEnumerable<TupleInput> tuples, string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Writes = tuples.Select(t => new ClientTupleKey
            {
                User = t.User,
                Relation = t.Relation,
                Object = t.Object
            }).ToList()
        }, Idempotent, cancellationToken: ct);
    }

    public async Task DeleteAsync(
        string storeId, string user, string relation, string @object, string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Deletes = [new ClientTupleKeyWithoutCondition { User = user, Relation = relation, Object = @object }]
        }, Idempotent, cancellationToken: ct);
    }

    public async Task DeleteBatchAsync(
        string storeId, IEnumerable<TupleInput> tuples, string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Deletes = tuples.Select(t => new ClientTupleKeyWithoutCondition
            {
                User = t.User,
                Relation = t.Relation,
                Object = t.Object
            }).ToList()
        }, Idempotent, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<StoredTuple>> ReadAsync(
        string storeId, string? user = null, string? relation = null, string? @object = null,
        CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId);
        var response = await client.Read(new ClientReadRequest
        {
            User = user,
            Relation = relation,
            Object = @object
        }, cancellationToken: ct);

        return response.Tuples?
            .Select(t => new StoredTuple(t.Key.User, t.Key.Relation, t.Key.Object))
            .ToList() ?? [];
    }

    public async Task<ReadPage> ReadPageAsync(
        string storeId, string? user, string? relation, string? @object,
        int pageSize, string? continuationToken, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId);
        var response = await client.Read(
            new ClientReadRequest { User = user, Relation = relation, Object = @object },
            new ClientReadOptions
            {
                PageSize = pageSize > 0 ? pageSize : null,
                ContinuationToken = string.IsNullOrEmpty(continuationToken) ? null : continuationToken
            },
            cancellationToken: ct);

        var tuples = response.Tuples?
            .Select(t => new StoredTuple(t.Key.User, t.Key.Relation, t.Key.Object))
            .ToList() ?? [];

        return new ReadPage(
            tuples,
            string.IsNullOrEmpty(response.ContinuationToken) ? null : response.ContinuationToken);
    }

    public async Task<bool> CheckAsync(
        string storeId, string user, string relation, string @object,
        string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        var response = await client.Check(new ClientCheckRequest
        {
            User = user,
            Relation = relation,
            Object = @object
        }, cancellationToken: ct);

        return response.Allowed ?? false;
    }

    public async Task<IReadOnlyList<BatchCheckResult>> BatchCheckAsync(
        string storeId, IEnumerable<CheckInput> checks,
        string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);

        var items = checks.Select((c, i) => new ClientBatchCheckItem
        {
            User = c.User,
            Relation = c.Relation,
            Object = c.Object,
            CorrelationId = c.CorrelationId ?? i.ToString()
        }).ToList();

        var response = await client.BatchCheck(new ClientBatchCheckRequest { Checks = items }, cancellationToken: ct);

        return response.Result
            .Select(r => new BatchCheckResult(r.CorrelationId, r.Allowed))
            .ToList();
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(
        string storeId, string user, string relation, string objectType,
        string? modelId = null, CancellationToken ct = default)
    {
        var client = clientProvider.GetClientForStore(storeId, modelId);
        var response = await client.ListObjects(new ClientListObjectsRequest
        {
            User = user,
            Relation = relation,
            Type = objectType
        }, cancellationToken: ct);

        return response.Objects ?? [];
    }

    public async Task<IReadOnlyList<string>> ListUsersAsync(
        string storeId, string @object, string relation, string userType,
        string? modelId = null, CancellationToken ct = default)
    {
        var (objectType, objectId) = SplitObject(@object);

        var client = clientProvider.GetClientForStore(storeId, modelId);
        var response = await client.ListUsers(new ClientListUsersRequest
        {
            Object = new FgaObject { Type = objectType, Id = objectId },
            Relation = relation,
            UserFilters = [new UserTypeFilter { Type = userType }]
        }, cancellationToken: ct);

        return response.Users?
            .Where(u => u.Object is not null)
            .Select(u => $"{u.Object.Type}:{u.Object.Id}")
            .ToList() ?? [];
    }

    private static (string Type, string Id) SplitObject(string @object)
    {
        var separator = @object.IndexOf(':');

        if (separator <= 0 || separator == @object.Length - 1)
            throw new ArgumentException("Nesne 'tip:id' biçiminde olmalıdır.", nameof(@object));

        return (@object[..separator], @object[(separator + 1)..]);
    }
}

public readonly record struct TupleInput(string User, string Relation, string Object);
public readonly record struct CheckInput(string User, string Relation, string Object, string? CorrelationId = null);
public readonly record struct StoredTuple(string User, string Relation, string Object);
public readonly record struct ReadPage(IReadOnlyList<StoredTuple> Tuples, string? ContinuationToken);
public readonly record struct BatchCheckResult(string CorrelationId, bool Allowed);