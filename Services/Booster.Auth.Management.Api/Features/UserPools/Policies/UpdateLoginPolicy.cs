using Booster.Auth.Common.Internal.Constants;
using Booster.Auth.Common.Internal.EntityExtensions;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Management.Api.Constants;
using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Management.Api.Features._base.Entities;
using Booster.Auth.Management.Api.Stores.ClientDataStores;
using Booster.Authorization.Fga.Attributes;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Mapster;

namespace Booster.Auth.Management.Api.Features.UserPools.Policies;

public class UpdateLoginPolicyEndpoint : Endpoint<UpdateLoginPolicyCommand, UserPoolPolicyChangeResponse>
{
    public override void Configure()
    {
        Put("policy/login");
        Group<UserPoolsEndpointGroup>();

        Options(x => x.RequireAuthorization(
            new RequireScopeAttribute(ManagementPolicies.PanelScope),
            new RequireFgaAttribute(AuthConstants.PoolType, ManagementPolicies.PoolTarget, AuthConstants.ManagerRelation)));

        Summary(s => s.Summary = "Havuzun giriş politikasını günceller");
    }

    public override async Task HandleAsync(UpdateLoginPolicyCommand req, CancellationToken ct)
        => await Send.OkAsync(await req.ExecuteAsync(ct), ct);
}

public record UpdateLoginPolicyCommand(
    Guid UserPoolId,
    LoginProviderType AllowedLoginProviders,
    List<IdentifierType> AllowedIdentifiers,
    int MaxFailedAttempts,
    int LockoutSeconds,
    string? DefaultCountryCode) : ICommand<UserPoolPolicyChangeResponse>;

public class UpdateLoginPolicyCommandHandler(
    IAsyncRepository<UserPool, AuthNDbContext> userPoolRepository,
    IClientDataCacheStore clientDataCacheStore)
    : ICommandHandler<UpdateLoginPolicyCommand, UserPoolPolicyChangeResponse>
{
    public async Task<UserPoolPolicyChangeResponse> ExecuteAsync(UpdateLoginPolicyCommand command, CancellationToken ct)
    {
        var pool = await userPoolRepository.GetAsync(p => p.Id == command.UserPoolId, cancellationToken: ct);
        pool.ThrowCustomExceptionIfDataNull(AuthNMessageKeys.UserPoolNotFound);

        var policy = pool!.Policy.Adapt<UserPoolPolicy>();
        policy.Login = command.Adapt<LoginPolicy>();
        policy.Login.AllowedIdentifiers = command.AllowedIdentifiers.Distinct().ToList();
        policy.Login.DefaultCountryCode = string.IsNullOrWhiteSpace(command.DefaultCountryCode) ? null : command.DefaultCountryCode.Trim();
        pool.Policy = policy;

        await userPoolRepository.SaveChangesAsync(ct);
        await clientDataCacheStore.InvalidatePoolAsync(pool.Id, ct);

        return new UserPoolPolicyChangeResponse(pool.Id);
    }
}

public class UpdateLoginPolicyValidator : Validator<UpdateLoginPolicyCommand>
{
    public UpdateLoginPolicyValidator()
    {
        RuleFor(x => x.UserPoolId).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.AllowedLoginProviders)
            .Must(v => v != LoginProviderType.None && (v & ~AllProviders) == 0)
            .WithMessage(AuthNMessageKeys.LoginProviderRequired);
        RuleFor(x => x.AllowedIdentifiers)
            .NotEmpty().WithMessage(AuthNMessageKeys.LoginIdentifierRequired);
        RuleForEach(x => x.AllowedIdentifiers).IsInEnum().WithMessage(ValidationErrors.InvalidType);
        RuleFor(x => x.MaxFailedAttempts).InclusiveBetween(1, 100).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.LockoutSeconds).InclusiveBetween(0, 2592000).WithMessage(AuthNMessageKeys.ValueOutOfRange);
        RuleFor(x => x.DefaultCountryCode)
            .Matches("^[0-9]{1,4}$").When(x => !string.IsNullOrWhiteSpace(x.DefaultCountryCode))
            .WithMessage(AuthNMessageKeys.CountryCodeInvalid);
    }

    private const LoginProviderType AllProviders = LoginProviderType.Local | LoginProviderType.Ldap
        | LoginProviderType.Federation | LoginProviderType.EmailCode | LoginProviderType.PhoneCode;
}
