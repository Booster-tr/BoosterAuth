using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Management.Api.Features.Tenants.GetById;

public class GetByIdTenantEndpoint : EndpointWithoutRequest<GetByIdTenantResponse>
{
    public override void Configure()
    {
        Get("detail");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.ViewerRelation)));

        Summary(s =>
        {
            s.Summary = "Tenant getirir";
            s.Description = "TenantId sorgu parametresiyle tek bir kiracının detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdTenantQuery(Query<Guid>("TenantId")).ExecuteAsync(ct), ct);
}

public record GetByIdTenantQuery(Guid TenantId) : ICommand<GetByIdTenantResponse>;

public record GetByIdTenantResponse(
    Guid Id,
    string Name,
    Guid OwnerId,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime,
    IReadOnlyList<TenantUserPoolResponse> UserPools);

public record TenantUserPoolResponse(
    Guid Id,
    string Name,
    EntityStatus Status,
    DateTimeOffset CreatedTime,
    DateTimeOffset? UpdatedTime);

public class GetByIdTenantQueryHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<GetByIdTenantQuery, GetByIdTenantResponse>
{
    public async Task<GetByIdTenantResponse> ExecuteAsync(GetByIdTenantQuery command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(
            t => t.Id == command.TenantId,
            include: q => q.Include(t => t.UserPools.OrderBy(p => p.Name)),
            enableTracking: false,
            cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        return tenant!.Adapt<GetByIdTenantResponse>();
    }
}
