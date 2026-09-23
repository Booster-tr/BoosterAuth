using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Database.Repositories;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using FastEndpoints;
using FluentValidation;

namespace Booster.Auth.Management.Api.Features.UserPools.UpdateDefaultScopes;

public class UpdateUserPoolDefaultScopesEndpoint : Endpoint<UpdateUserPoolDefaultScopesCommand, UpdateUserPoolDefaultScopesResponse>
{
    public override void Configure()
    {
        Put("{Id:guid}/default-scopes");
        Group<UserPoolsEndpointGroup>();
        Summary(s =>
        {
            s.Summary = "Havuzun varsayılan scope listesini günceller";
            s.Description = "Bu havuza kayıt olan kullanıcılara otomatik atanacak izinleri belirler. " +
                            "Her scope havuzda tanımlı (ScopeDefinition) olmalıdır.";
        });
    }

    public override async Task HandleAsync(UpdateUserPoolDefaultScopesCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateUserPoolDefaultScopesCommand(Guid Id, List<string> Scopes) : ICommand<UpdateUserPoolDefaultScopesResponse>;

public readonly record struct UpdateUserPoolDefaultScopesResponse(Guid Id);

public class UpdateUserPoolDefaultScopesCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IAsyncRepository<ScopeDefinition, AuthNDbContext> scopeDefinitionRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateUserPoolDefaultScopesCommand, UpdateUserPoolDefaultScopesResponse>
{
    public async Task<UpdateUserPoolDefaultScopesResponse> ExecuteAsync(
        UpdateUserPoolDefaultScopesCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.Id, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var distinctScopes = command.Scopes
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (distinctScopes.Count > 0)
        {
            var definedCount = await scopeDefinitionRepository.CountAsync(
                s => s.UserPoolId == command.Id && distinctScopes.Contains(s.Scope),
                cancellationToken: ct);

            if (definedCount != distinctScopes.Count)
                throw new BusinessException(AuthNMessageKeys.ScopeNotDefinedInPool);
        }

        pool!.DefaultScopes = distinctScopes;

        await userPoolRepository.SaveChangesAsync(ct);

        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UpdateUserPoolDefaultScopesResponse(pool.Id);
    }
}

public class UpdateUserPoolDefaultScopesValidator : Validator<UpdateUserPoolDefaultScopesCommand>
{
    public UpdateUserPoolDefaultScopesValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.Scopes).NotNull().WithMessage(ValidationErrors.NotNullOrEmptyError);
    }
}
