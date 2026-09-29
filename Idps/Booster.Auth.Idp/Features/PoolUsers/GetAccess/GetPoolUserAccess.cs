using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features.PoolUsers.GetAccess;

public class GetPoolUserAccessEndpoint : EndpointWithoutRequest<PoolUserAccessResponse>
{
    public override void Configure()
    {
        Get("pools/users/access");

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(AuthConstants.ManagementPanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, IdpPolicies.PoolTarget, AuthConstants.ViewerRelation)));

        Summary(s => s.Summary = "Havuz kullanıcısının rol ve kapsamlarını döner");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new GetPoolUserAccessQuery(Query<Guid>("UserPoolId"), Query<Guid>("PrincipalId"));

        await Send.OkAsync(await query.ExecuteAsync(ct), ct);
    }
}

public record GetPoolUserAccessQuery(Guid UserPoolId, Guid PrincipalId) : ICommand<PoolUserAccessResponse>;

public record PoolUserAccessResponse(IReadOnlyList<Guid> RoleIds, IReadOnlyList<string> Scopes);

public class GetPoolUserAccessQueryHandler(
    IAsyncRepository<Principal, IdpDbContext> principalRepository,
    IAsyncRepository<PrincipalRole, IdpDbContext> principalRoleRepository,
    IAsyncRepository<PrincipalScope, IdpDbContext> principalScopeRepository,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<GetPoolUserAccessQuery, PoolUserAccessResponse>
{
    public async Task<PoolUserAccessResponse> ExecuteAsync(GetPoolUserAccessQuery command, CancellationToken ct)
    {
        using var _ = requestContextAccessor.BeginSystemContext(command.UserPoolId);

        var exists = await principalRepository.AnyAsync(
            p => p.Id == command.PrincipalId && p.Type == PrincipalType.User, cancellationToken: ct);

        if (!exists)
            throw new BusinessException(IdpMessageKeys.PrincipalNotFound);

        var roleIds = await principalRoleRepository.Query()
            .Where(r => r.PrincipalId == command.PrincipalId)
            .Select(r => r.RoleId)
            .ToListAsync(ct);

        var scopes = await principalScopeRepository.Query()
            .Where(s => s.PrincipalId == command.PrincipalId)
            .Select(s => s.Scope)
            .ToListAsync(ct);

        return new PoolUserAccessResponse(roleIds, scopes);
    }
}
