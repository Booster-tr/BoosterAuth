using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.Create;

public class CreateScopeDefinitionEndpoint : Endpoint<CreateScopeDefinitionCommand, CreateScopeDefinitionResponse>
{
    public override void Configure()
    {
        Post("");
        Group<ScopeDefinitionsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.GrantManagerRelation)));

        Summary(s =>
        {
            s.Summary = "Yeni kapsam tanımı oluşturur";
            s.Description = "Havuz altında API erişim izni tanımlar. Tanım havuz içinde benzersizdir.";
        });
    }

    public override async Task HandleAsync(CreateScopeDefinitionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record CreateScopeDefinitionCommand(
    Guid UserPoolId,
    string Scope,
    string DisplayName,
    string? Description) : ICommand<CreateScopeDefinitionResponse>;

public readonly record struct CreateScopeDefinitionResponse(Guid Id);

public class CreateScopeDefinitionCommandHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository,
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<CreateScopeDefinitionCommand, CreateScopeDefinitionResponse>
{
    public async Task<CreateScopeDefinitionResponse> ExecuteAsync(
        CreateScopeDefinitionCommand command, CancellationToken ct)
    {
        var poolExists = await userPoolRepository.AnyAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);

        if (!poolExists)
            throw new BusinessException(AuthNMessageKeys.UserPoolNotFound);

        command = command with { Scope = command.Scope.Trim().ToLowerInvariant() };

        var scopeExists = await scopeRepository.AnyAsync(
            s => s.UserPoolId == command.UserPoolId && s.Scope == command.Scope, cancellationToken: ct);

        if (scopeExists)
            throw new BusinessException(AuthNMessageKeys.ScopeAlreadyDefined);

        var scope = command.Adapt<ScopeDefinition>();
        scope.SetId();

        await scopeRepository.AddAsync(scope, ct);
        await scopeRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(command.UserPoolId, ct);

        return new CreateScopeDefinitionResponse(scope.Id);
    }
}

public class CreateScopeDefinitionValidator : Validator<CreateScopeDefinitionCommand>
{
    public CreateScopeDefinitionValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(200).WithMessage(ValidationErrors.MaxLengthError)
            .Matches("^[a-zA-Z0-9:_.-]+$").WithMessage(AuthNMessageKeys.ScopeInvalidFormat);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);

        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
    }
}
