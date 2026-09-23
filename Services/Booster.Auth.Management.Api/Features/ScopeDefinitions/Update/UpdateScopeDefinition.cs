using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.ScopeDefinitions.Update;

public class UpdateScopeDefinitionEndpoint : Endpoint<UpdateScopeDefinitionCommand, UpdateScopeDefinitionResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}");
        Group<ScopeDefinitionsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Scope tanımını günceller";
            s.Description = "Görünen ad ve açıklamayı günceller. Scope adı ve havuzu değiştirilemez.";
        });
    }

    public override async Task HandleAsync(UpdateScopeDefinitionCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateScopeDefinitionCommand(
    Guid Id,
    string DisplayName,
    string? Description) : ICommand<UpdateScopeDefinitionResponse>;

public readonly record struct UpdateScopeDefinitionResponse(Guid Id);

public class UpdateScopeDefinitionCommandHandler(
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeRepository)
    : ICommandHandler<UpdateScopeDefinitionCommand, UpdateScopeDefinitionResponse>
{
    public async Task<UpdateScopeDefinitionResponse> ExecuteAsync(
        UpdateScopeDefinitionCommand command, CancellationToken ct)
    {
        var scope = await scopeRepository.GetAsync(s => s.Id == command.Id, cancellationToken: ct);
        scope.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.ScopeDefinitionNotFound);

        command.Adapt(scope);

        await scopeRepository.SaveChangesAsync(ct);

        return new UpdateScopeDefinitionResponse(scope!.Id);
    }
}

public class UpdateScopeDefinitionValidator : Validator<UpdateScopeDefinitionCommand>
{
    public UpdateScopeDefinitionValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .MaximumLength(300).WithMessage(ValidationErrors.MaxLengthError);
        RuleFor(x => x.Description).MaximumLength(1000).WithMessage(ValidationErrors.MaxLengthError);
    }
}
