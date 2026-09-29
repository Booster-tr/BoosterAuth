using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Features.PoolServiceAccounts._base;
using Booster.Auth.Management.Grpc;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.PoolServiceAccounts.GetAccess;

public class GetPoolServiceAccountAccessEndpoint : EndpointWithoutRequest<PoolServiceAccountAccessResponse>
{
    public override void Configure()
    {
        Get("pools/service-accounts/access");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Servis hesabının yetki, rol ve kapsamlarını döner";
            s.Description = "Seçenekler, yönetimin tanımları içinden çağıranın kendisinde olanlarla sınırlıdır.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetPoolServiceAccountAccessQuery(
            Query<Guid>("UserPoolId"),
            Query<Guid>("ServiceAccountId"),
            User.FindAll("role").Select(c => c.Value).ToList(),
            (User.FindFirst("scope")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetPoolServiceAccountAccessQuery(
    Guid UserPoolId,
    Guid ServiceAccountId,
    IReadOnlyList<string> CallerRoles,
    IReadOnlyList<string> CallerScopes) : ICommand<PoolServiceAccountAccessResponse>;

public record PoolServiceAccountAccessResponse(
    string? Relation,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<AssignableRoleResponse> AvailableRoles,
    IReadOnlyList<string> AvailableScopes);

public readonly record struct AssignableRoleResponse(Guid Id, string Key);

public class GetPoolServiceAccountAccessQueryHandler(
    IAsyncRepository<ServiceAccount, IdpDbContext> serviceAccountRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    AuthorizationService.AuthorizationServiceClient authorizationClient,
    ClientDataService.ClientDataServiceClient clientDataClient,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GetPoolServiceAccountAccessQuery, PoolServiceAccountAccessResponse>
{
    public async Task<PoolServiceAccountAccessResponse> ExecuteAsync(
        GetPoolServiceAccountAccessQuery command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(AuthConstants.ManagementPoolId);

        var account = await serviceAccountRepository.GetAsync(
            s => s.Id == command.ServiceAccountId && s.OwnerPoolId == command.UserPoolId,
            enableTracking: false, cancellationToken: ct)
            ?? throw new BusinessException(IdpMessageKeys.ServiceAccountNotFound);

        var roleIds = await principalRoleRepository.Query()
            .Where(r => r.PrincipalId == account.PrincipalId)
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        var scopes = await principalScopeRepository.Query()
            .Where(s => s.PrincipalId == account.PrincipalId)
            .Select(s => s.Scope)
            .ToListAsync(ct);

        var relations = await authorizationClient.ReadRelationsAsync(
            new ReadRelationsRequest
            {
                IsolationId = ServiceAccountRules.Isolation,
                User = AuthConstants.User(account.PrincipalId),
                Object = AuthConstants.Pool(command.UserPoolId)
            },
            cancellationToken: ct);

        var definitions = await clientDataClient.GetPoolDefinitionsAsync(
            new GetPoolDefinitionsRequest { UserPoolId = AuthConstants.ManagementPoolId.ToString() },
            cancellationToken: ct);

        var callerRoles = command.CallerRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var callerScopes = command.CallerScopes.ToHashSet(StringComparer.Ordinal);

        return new PoolServiceAccountAccessResponse(
            relations.Results.Select(r => r.Relation).FirstOrDefault(r => ServiceAccountRules.Assignable.Contains(r)),
            roleIds,
            scopes,
            definitions.Roles
                .Where(r => callerRoles.Contains(r.Key))
                .Select(r => new AssignableRoleResponse(Guid.Parse(r.Id), r.Key))
                .ToList(),
            definitions.Scopes.Where(callerScopes.Contains).ToList());
    }
}
