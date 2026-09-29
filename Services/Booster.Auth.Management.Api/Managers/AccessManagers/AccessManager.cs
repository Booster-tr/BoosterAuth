using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;

namespace Booster.Auth.Management.Api.Managers.AccessManagers;

public sealed class AccessManager(
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor,
    IServiceScopeFactory scopeFactory,
    ILogger<AccessManager> logger) : IAccessManager
{
    private static readonly string UserPrefix = $"{AuthConstants.UserPrefix}:";

    private const int MaxBatchSize = 100;

    public async Task<AccessPage> ListAsync(
        string objectType, Guid objectId, IReadOnlyList<string> relations,
        int pageSize, string? continuationToken, CancellationToken ct)
    {
        var size = Math.Clamp(pageSize, 1, MaxBatchSize);
        var (index, fgaToken) = ParseToken(continuationToken);
        var items = new List<AccessEntry>();

        while (index < relations.Count && items.Count < size)
        {
            var response = await authorizationClient.ReadRelationsAsync(
                new ReadRelationsRequest
                {
                    IsolationId = Isolation(),
                    Object = Target(objectType, objectId),
                    Relation = relations[index],
                    PageSize = size - items.Count,
                    ContinuationToken = fgaToken ?? string.Empty
                },
                cancellationToken: ct);

            items.AddRange(response.Results
                .Where(r => r.User.StartsWith(UserPrefix, StringComparison.Ordinal))
                .Select(r => (Id: Guid.TryParse(r.User[UserPrefix.Length..], out var id) ? id : Guid.Empty, r.Relation))
                .Where(r => r.Id != Guid.Empty)
                .Select(r => new AccessEntry(r.Id, r.Relation)));

            if (string.IsNullOrEmpty(response.ContinuationToken))
            {
                index++;
                fgaToken = null;
            }
            else
            {
                fgaToken = response.ContinuationToken;
            }
        }

        return new AccessPage(items, index < relations.Count ? FormatToken(index, fgaToken) : null);
    }

    private static string FormatToken(int index, string? fgaToken)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{index}|{fgaToken}"));

    private static (int Index, string? FgaToken) ParseToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return (0, null);

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var separator = raw.IndexOf('|');

            return separator > 0 && int.TryParse(raw[..separator], out var index) && index >= 0
                ? (index, raw[(separator + 1)..] is { Length: > 0 } fga ? fga : null)
                : (0, null);
        }
        catch (FormatException)
        {
            return (0, null);
        }
    }

    public async Task GrantAsync(
        string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct)
        => await authorizationClient.WriteRelationsAsync(
            Request(objectType, objectId, principalId, relation), cancellationToken: ct);

    public async Task RevokeAsync(
        string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct)
        => await authorizationClient.DeleteRelationsAsync(
            Request(objectType, objectId, principalId, relation), cancellationToken: ct);

    public void RevokeAllInBackground(string objectType, Guid objectId)
    {
        var target = Target(objectType, objectId);
        var isolation = Isolation();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var client = scope.ServiceProvider
                    .GetRequiredService<AuthorizationService.AuthorizationServiceClient>();

                while (true)
                {
                    var page = await client.ReadRelationsAsync(new ReadRelationsRequest
                    {
                        IsolationId = isolation,
                        Object = target,
                        PageSize = MaxBatchSize
                    });

                    if (page.Results.Count == 0)
                        return;

                    var request = new WriteRelationsRequest { IsolationId = isolation };
                    request.Tuples.AddRange(page.Results.Select(r => new RelationTuple
                    {
                        User = r.User,
                        Relation = r.Relation,
                        Object = r.Object
                    }));

                    await client.DeleteRelationsAsync(request);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Target} üzerindeki yetkiler temizlenemedi.", target);
            }
        });
    }

    public async Task<IReadOnlyList<Guid>> ListAccessibleIdsAsync(
        string objectType, string relation, CancellationToken ct)
    {
        var response = await authorizationClient.LookupByUserAsync(
            new LookupByUserRequest
            {
                IsolationId = Isolation(),
                User = AuthConstants.User(requestContextAccessor.Context().PrincipalId),
                ObjectType = objectType,
                Relation = relation
            },
            cancellationToken: ct);

        var prefix = $"{objectType}:";

        return response.Results
            .Select(r => r.Object)
            .Where(o => o.StartsWith(prefix, StringComparison.Ordinal))
            .Select(o => Guid.TryParse(o[prefix.Length..], out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
    }

    public async Task LinkParentAsync(
        string objectType, Guid objectId, string parentType, Guid parentId, CancellationToken ct)
        => await authorizationClient.WriteRelationsAsync(
            ParentRequest(objectType, objectId, parentType, parentId), cancellationToken: ct);

    private WriteRelationsRequest ParentRequest(
        string objectType, Guid objectId, string parentType, Guid parentId)
    {
        var request = new WriteRelationsRequest { IsolationId = Isolation() };

        request.Tuples.Add(new RelationTuple
        {
            User = Target(parentType, parentId),
            Relation = AuthConstants.ParentRelation,
            Object = Target(objectType, objectId)
        });

        return request;
    }

    private WriteRelationsRequest Request(
        string objectType, Guid objectId, Guid principalId, string relation)
    {
        var request = new WriteRelationsRequest { IsolationId = Isolation() };

        request.Tuples.Add(new RelationTuple
        {
            User = AuthConstants.User(principalId),
            Relation = relation,
            Object = Target(objectType, objectId)
        });

        return request;
    }

    private static string Target(string objectType, Guid objectId) => $"{objectType}:{objectId}";

    private string Isolation() => requestContextAccessor.Context().Isolation.ToString();
}
