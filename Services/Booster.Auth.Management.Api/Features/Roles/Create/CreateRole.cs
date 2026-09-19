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

namespace Booster.Auth.Management.Api.Features.Roles.Create;

public class CreateRoleEndpoint : Endpoint<CreateRoleCommand, CreateRoleResponse>
{
    public override void Configure()
    {
        Post("");
        Group<RolesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Yeni rol oluşturur";
            s.Description = "Bir kullanıcı havuzu altında rol tanımlar. Rol anahtarı havuz içinde benzersizdir.";
        });
    }

    public override async Task HandleAsync(CreateRoleCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateRoleCommand(
    Guid UserPoolId,
    string Key,
    string DisplayName,
    string? Description) : ICommand<CreateRoleResponse>;

public readonly record struct CreateRoleResponse(Guid Id);

public class CreateRoleCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository)
    : ICommandHandler<CreateRoleCommand, CreateRoleResponse>
{
    public async Task<CreateRoleResponse> ExecuteAsync(CreateRoleCommand command, CancellationToken ct)
    {
        var poolExists = await userPoolRepository.AnyAsync(
            p => p.Id == command.UserPoolId, cancellationToken: ct);

        if (!poolExists)
            throw new BusinessException(AuthNMessageKeys.UserPoolNotFound);

        command = command with { Key = command.Key.Trim().ToLowerInvariant() };

        // Benzersizlik havuz içindedir: aynı anahtar farklı havuzlarda yaşayabilir.
        var keyExists = await roleRepository.AnyAsync(
            r => r.UserPoolId == command.UserPoolId && r.Key == command.Key, cancellationToken: ct);

        if (keyExists)
            throw new BusinessException(AuthNMessageKeys.RoleKeyAlreadyExists);

        var role = command.Adapt<Role>();
        role.SetId();

        await roleRepository.AddAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);

        return new CreateRoleResponse(role.Id);
    }
}

public class CreateRoleValidator : Validator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Key)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-z0-9-]+$").WithMessage(AuthNMessageKeys.RoleKeyInvalidFormat);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
    }
}
