using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;

namespace Booster.Auth.Management.Api.Managers.AccessManagers;

public sealed class AccessManager(
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    IRequestContextAccessor requestContextAccessor) : IAccessManager
{
    private static readonly string UserPrefix = $"{AuthConstants.UserPrefix}:";

    public async Task<IReadOnlyList<AccessEntry>> ListAsync(
        string objectType, Guid objectId, CancellationToken ct)
    {
        var granted = await ReadGrantedAsync(Target(objectType, objectId), ct);

        return granted
            .OrderBy(pair => pair.Key)
            .Select(pair => new AccessEntry(pair.Key, pair.Value))
            .ToList();
    }

    public async Task GrantAsync(
        string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct)
        => await authorizationClient.WriteRelationsAsync(
            Request(objectType, objectId, principalId, relation), cancellationToken: ct);

    public async Task RevokeAsync(
        string objectType, Guid objectId, Guid principalId, string relation, CancellationToken ct)
        => await authorizationClient.DeleteRelationsAsync(
            Request(objectType, objectId, principalId, relation), cancellationToken: ct);

    public async Task RevokeAllAsync(string objectType, Guid objectId, CancellationToken ct)
    {
        var target = Target(objectType, objectId);

        var granted = await ReadGrantedAsync(target, ct);

        var request = new WriteRelationsRequest { IsolationId = Isolation() };

        foreach (var (principalId, relations) in granted)
        {
            foreach (var relation in relations)
            {
                request.Tuples.Add(new RelationTuple
                {
                    User = AuthConstants.User(principalId),
                    Relation = relation,
                    Object = target
                });
            }
        }

        if (request.Tuples.Count == 0)
            return;

        await authorizationClient.DeleteRelationsAsync(request, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Guid>> ListAccessibleIdsAsync(string objectType, CancellationToken ct)
    {
        var response = await authorizationClient.LookupByUserAsync(
            new LookupByUserRequest
            {
                IsolationId = Isolation(),
                User = AuthConstants.User(requestContextAccessor.Context().PrincipalId),
                ObjectType = objectType,
                Relation = AuthConstants.ViewerRelation
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

    public async Task UnlinkParentAsync(
        string objectType, Guid objectId, string parentType, Guid parentId, CancellationToken ct)
        => await authorizationClient.DeleteRelationsAsync(
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

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> ReadGrantedAsync(
        string target, CancellationToken ct)
    {
        var response = await authorizationClient.ReadRelationsAsync(
            new ReadRelationsRequest
            {
                IsolationId = Isolation(),
                User = string.Empty,
                Relation = string.Empty,
                Object = target
            },
            cancellationToken: ct);

        return Group(response.Results.Select(r => (r.User, r.Relation)));
    }

    private static Dictionary<Guid, IReadOnlyList<string>> Group(
        IEnumerable<(string User, string Relation)> tuples)
        => tuples
            .Where(t => t.User.StartsWith(UserPrefix, StringComparison.Ordinal))
            .Select(t => (Id: t.User[UserPrefix.Length..], t.Relation))
            .Where(t => Guid.TryParse(t.Id, out _))
            .GroupBy(t => Guid.Parse(t.Id))
            .ToDictionary(
                group => group.Key,
                IReadOnlyList<string> (group) => group
                    .Select(t => t.Relation)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToList());

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
