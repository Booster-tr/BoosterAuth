using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;

namespace Booster.Auth.Management.Api.Features.Tenants.Delete;

public class DeleteTenantEndpoint : EndpointWithoutRequest<DeleteTenantResponse>
{
    public override void Configure()
    {
        Delete("");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.OwnerRelation)));

        Summary(s =>
        {
            s.Summary = "Tenant siler";
            s.Description = "Kiracıyı soft-delete ile kaldırır, üzerindeki yetkileri de temizler.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await new DeleteTenantCommand(Query<Guid>("TenantId")).ExecuteAsync(ct), ct);
}

public record DeleteTenantCommand(Guid TenantId) : ICommand<DeleteTenantResponse>;

public readonly record struct DeleteTenantResponse(Guid Id);

public class DeleteTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAccessManager accessManager)
    : ICommandHandler<DeleteTenantCommand, DeleteTenantResponse>
{
    public async Task<DeleteTenantResponse> ExecuteAsync(DeleteTenantCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(t => t.Id == command.TenantId, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        tenantRepository.Delete(tenant!);
        await tenantRepository.SaveChangesAsync(ct);

        await accessManager.RevokeAllAsync(AuthConstants.TenantType, command.TenantId, ct);

        return new DeleteTenantResponse(tenant!.Id);
    }
}
