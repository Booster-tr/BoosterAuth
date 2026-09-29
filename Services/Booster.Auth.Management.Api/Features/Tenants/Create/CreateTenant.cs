using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Managers.AccessManagers;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Tenants.Create;

public class CreateTenantEndpoint : Endpoint<CreateTenantCommand, CreateTenantResponse>
{
    public override void Configure()
    {
        Post("");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.IsolationTarget, AuthConstants.EndUserRelation)));

        Summary(s =>
        {
            s.Summary = "Yeni tenant oluşturur";
            s.Description = "Kiracı kaydını açar ve oluşturana o kiracı üzerinde owner yetkisi yazar.";
        });
    }

    public override async Task HandleAsync(CreateTenantCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateTenantCommand(string Name) : ICommand<CreateTenantResponse>;

public readonly record struct CreateTenantResponse(Guid Id);

public class CreateTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository,
    IAccessManager accessManager,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<CreateTenantCommand, CreateTenantResponse>
{
    public async Task<CreateTenantResponse> ExecuteAsync(CreateTenantCommand command, CancellationToken ct)
    {
        var ownerId = requestContextAccessor.Context().PrincipalId;

        var tenant = command.Adapt<Tenant>();
        tenant.SetId();
        tenant.OwnerId = ownerId;

        await tenantRepository.AddAsync(tenant, ct);
        await tenantRepository.SaveChangesAsync(ct);

        await accessManager.GrantAsync(
            AuthConstants.TenantType,
            tenant.Id,
            ownerId,
            AuthConstants.OwnerRelation,
            ct);

        return new CreateTenantResponse(tenant.Id);
    }
}

public class CreateTenantValidator : Validator<CreateTenantCommand>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
    }
}
