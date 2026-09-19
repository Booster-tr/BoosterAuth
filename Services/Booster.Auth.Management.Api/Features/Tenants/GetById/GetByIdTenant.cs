using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Tenants.GetById;

public class GetByIdTenantEndpoint : EndpointWithoutRequest<GetByIdTenantResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<TenantsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Tenant getirir";
            s.Description = "Id ile tek bir kiracının detayını döner.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new GetByIdTenantQuery(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record GetByIdTenantQuery(Guid Id) : ICommand<GetByIdTenantResponse>;

public readonly record struct GetByIdTenantResponse(Guid Id, string Name);

public class GetByIdTenantQueryHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<GetByIdTenantQuery, GetByIdTenantResponse>
{
    public async Task<GetByIdTenantResponse> ExecuteAsync(GetByIdTenantQuery command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(
            t => t.Id == command.Id, enableTracking: false, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        return tenant.Adapt<GetByIdTenantResponse>();
    }
}
