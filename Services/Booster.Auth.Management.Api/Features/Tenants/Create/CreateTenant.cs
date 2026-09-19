using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
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
        Summary(s =>
        {
            s.Summary = "Yeni tenant oluşturur";
            s.Description = "Kiracı kaydı açar.";
        });
    }

    public override async Task HandleAsync(CreateTenantCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateTenantCommand(string Name) : ICommand<CreateTenantResponse>;

public readonly record struct CreateTenantResponse(Guid Id);

public class CreateTenantCommandHandler(
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<CreateTenantCommand, CreateTenantResponse>
{
    public async Task<CreateTenantResponse> ExecuteAsync(CreateTenantCommand command, CancellationToken ct)
    {
        var tenant = command.Adapt<Tenant>();
        tenant.SetId();

        await tenantRepository.AddAsync(tenant, ct);
        await tenantRepository.SaveChangesAsync(ct);

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
