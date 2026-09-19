using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.Tenants.Update;

public class UpdateTenantEndpoint : Endpoint<UpdateTenantCommand, UpdateTenantResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}");
        Group<TenantsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Tenant bilgisini günceller";
            s.Description = "Kiracının görünen adını günceller.";
        });
    }

    public override async Task HandleAsync(UpdateTenantCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateTenantCommand(Guid Id, string Name) : ICommand<UpdateTenantResponse>;

public readonly record struct UpdateTenantResponse(Guid Id);

public class UpdateTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<UpdateTenantCommand, UpdateTenantResponse>
{
    public async Task<UpdateTenantResponse> ExecuteAsync(UpdateTenantCommand command, CancellationToken ct)
    {
        var tenant = await tenantRepository.GetAsync(t => t.Id == command.Id, cancellationToken: ct);
        tenant.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.TenantNotFound);

        command.Adapt(tenant);

        await tenantRepository.SaveChangesAsync(ct);

        return new UpdateTenantResponse(tenant!.Id);
    }
}

public class UpdateTenantValidator : Validator<UpdateTenantCommand>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
    }
}
