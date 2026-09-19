using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Tenants.Delete;

public class DeleteTenantEndpoint : EndpointWithoutRequest<DeleteTenantResponse>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<TenantsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Tenant siler";
            s.Description = "Kiracıyı soft-delete ile kaldırır. Havuzlar ve altındaki kayıtlar cascade ile düşer.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteTenantCommand(Route<Guid>("id")).ExecuteAsync(ct), ct);
}

public record DeleteTenantCommand(Guid Id) : ICommand<DeleteTenantResponse>;

public readonly record struct DeleteTenantResponse(Guid Id);

public class DeleteTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<DeleteTenantCommand, DeleteTenantResponse>
{
    public async Task<DeleteTenantResponse> ExecuteAsync(DeleteTenantCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(t => t.Id == command.Id, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        tenantRepository.Delete(tenant!);
        await tenantRepository.SaveChangesAsync(ct);

        return new DeleteTenantResponse(tenant!.Id);
    }
}
