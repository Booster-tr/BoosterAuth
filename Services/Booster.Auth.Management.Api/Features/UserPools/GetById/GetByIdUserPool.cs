using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Booster.Auth.Common.Internal.Constants;
using Booster.Authorization.Fga.Attributes;
using FastEndpoints;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.UserPools.GetById;

public class GetByIdUserPoolEndpoint : EndpointWithoutRequest<GetByIdUserPoolResponse>
{
    public override void Configure()
    {
        Get("detail");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ViewerRelation)));
        Summary(s =>
        {
            s.Summary = "Kullanıcı havuzunu getirir";
            s.Description = "Havuzun detayını politikaları ve varsayılanları ile birlikte döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdUserPoolQuery(Query<Guid>("UserPoolId")).ExecuteAsync(ct), ct);
}

public record GetByIdUserPoolQuery(Guid UserPoolId) : ICommand<GetByIdUserPoolResponse>;

public record GetByIdUserPoolResponse(
    Guid Id,
    string Name,
    EntityStatus Status,
    Guid TenantId,
    string TenantName,
    UserPoolPolicy Policy,
    List<string> DefaultScopes,
    List<Guid> DefaultRoleIds,
    IReadOnlyList<UserPoolRoleResponse> Roles,
    IReadOnlyList<UserPoolScopeResponse> ScopeDefinitions,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime);

public record UserPoolRoleResponse(Guid Id, string Key, string DisplayName);

public record UserPoolScopeResponse(string Scope, string DisplayName);

public class GetByIdUserPoolQueryHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<GetByIdUserPoolQuery, GetByIdUserPoolResponse>
{
    public async Task<GetByIdUserPoolResponse> ExecuteAsync(GetByIdUserPoolQuery command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(
            p => p.Id == command.UserPoolId,
            include: q => q
                .Include(p => p.Tenant)
                .Include(p => p.Roles.OrderBy(r => r.DisplayName))
                .Include(p => p.ScopeDefinitions.OrderBy(s => s.Scope)),
            enableTracking: false,
            cancellationToken: ct);

        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        return pool!.Adapt<GetByIdUserPoolResponse>();
    }
}
