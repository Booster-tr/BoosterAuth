using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.RoleScopes.Add;

public class AddRoleScopeEndpoint : Endpoint<AddRoleScopeCommand, AddRoleScopeResponse>
{
    public override void Configure()
    {
        Post("");
        Group<RoleScopesEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Role scope ekler";
            s.Description = "Bir role, kendi havuzunda tanımlı bir erişim iznini ekler.";
        });
    }

    public override async Task HandleAsync(AddRoleScopeCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record AddRoleScopeCommand(Guid RoleId, string Scope) : ICommand<AddRoleScopeResponse>;

public readonly record struct AddRoleScopeResponse(Guid Id);

public class AddRoleScopeCommandHandler(
    IAsyncRepository<Role, AuthNDbContext> roleRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IAsyncRepository<RoleScope, AuthNDbContext> roleScopeRepository)
    : ICommandHandler<AddRoleScopeCommand, AddRoleScopeResponse>
{
    public async Task<AddRoleScopeResponse> ExecuteAsync(AddRoleScopeCommand command, CancellationToken ct)
    {
        var role = await roleRepository.GetAsync(
            r => r.Id == command.RoleId, enableTracking: false, cancellationToken: ct);
        role.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.RoleNotFound);

        command = command with { Scope = command.Scope.Trim().ToLowerInvariant() };

        // Rolün kendi havuzunda tanımlı olmayan bir izin eklenemez; aksi hâlde
        // token üretiminde karşılığı olmayan bir izin taşınır.
        var isDefined = await scopeDefinitionRepository.AnyAsync(
            s => s.UserPoolId == role!.UserPoolId && s.Scope == command.Scope, cancellationToken: ct);

        if (!isDefined)
            throw new BusinessException(AuthNMessageKeys.ScopeNotDefinedInPool);

        var alreadyAdded = await roleScopeRepository.AnyAsync(
            x => x.RoleId == command.RoleId && x.Scope == command.Scope, cancellationToken: ct);

        if (alreadyAdded)
            throw new BusinessException(AuthNMessageKeys.RoleScopeAlreadyAdded);

        var entity = command.Adapt<RoleScope>();
        entity.SetId();

        await roleScopeRepository.AddAsync(entity, ct);
        await roleScopeRepository.SaveChangesAsync(ct);

        return new AddRoleScopeResponse(entity.Id);
    }
}

public class AddRoleScopeValidator : Validator<AddRoleScopeCommand>
{
    public AddRoleScopeValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(200).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-zA-Z0-9:_.-]+$").WithMessage(AuthNMessageKeys.ScopeInvalidFormat);
    }
}
