using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.Tenants.Update;

public class UpdateTenantEndpoint : Endpoint<UpdateTenantCommand, UpdateTenantResponse>
{
    public override void Configure()
    {
        Put("");
        Group<TenantsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.TenantType, ManagementPolicies.TenantTarget, AuthConstants.ManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Tenant bilgisini günceller";
            s.Description = "TenantId sorgudan, güncellenecek alanlar gövdeden gelir.";
        });
    }

    public override async Task HandleAsync(UpdateTenantCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateTenantCommand(Guid TenantId, string Name) : ICommand<UpdateTenantResponse>;

public readonly record struct UpdateTenantResponse(Guid Id);

public class UpdateTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<UpdateTenantCommand, UpdateTenantResponse>
{
    public async Task<UpdateTenantResponse> ExecuteAsync(UpdateTenantCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(t => t.Id == command.TenantId, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        tenant!.Name = command.Name;

        await tenantRepository.SaveChangesAsync(ct);

        return new UpdateTenantResponse(tenant.Id);
    }
}

public class UpdateTenantValidator : Validator<UpdateTenantCommand>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
    }
}
