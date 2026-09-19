using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.UserPools.Create;

public class CreateUserPoolEndpoint : Endpoint<CreateUserPoolCommand, CreateUserPoolResponse>
{
    public override void Configure()
    {
        Post("");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Yeni kullanıcı havuzu (UserPool) oluşturur";
            s.Description = "Verilen tenant altında, varsayılan politikalarla bir kimlik havuzu kaydeder. " +
                            "Tenant istekten gelir; oturum bağlamından çözülmez.";
        });
    }

    public override async Task HandleAsync(CreateUserPoolCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateUserPoolCommand(Guid TenantId, string Name) : ICommand<CreateUserPoolResponse>;

public readonly record struct CreateUserPoolResponse(Guid Id);

public class CreateUserPoolCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<Tenant, AuthNDbContext> tenantRepository)
    : ICommandHandler<CreateUserPoolCommand, CreateUserPoolResponse>
{
    public async Task<CreateUserPoolResponse> ExecuteAsync(CreateUserPoolCommand command, CancellationToken ct)
    {
        var tenantExists = await tenantRepository.AnyAsync(
            t => t.Id == command.TenantId, cancellationToken: ct);

        if (!tenantExists)
            throw new BusinessException(AuthNMessageKeys.TenantNotFound);

        var pool = command.Adapt<UserPool>();
        pool.SetId();
        pool.Status = EntityStatus.Active;
        pool.Policy = new UserPoolPolicy();

        await userPoolRepository.AddAsync(pool, ct);
        await userPoolRepository.SaveChangesAsync(ct);

        return new CreateUserPoolResponse(pool.Id);
    }
}

public class CreateUserPoolValidator : Validator<CreateUserPoolCommand>
{
    public CreateUserPoolValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
    }
}
