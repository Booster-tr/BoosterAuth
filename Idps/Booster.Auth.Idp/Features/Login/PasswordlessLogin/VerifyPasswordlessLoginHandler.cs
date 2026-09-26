using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Booster.Auth.Idp.Features.Login.PasswordlessLogin;

public sealed class VerifyPasswordlessLoginHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    IClientDataStore clientDataStore,
    IProviderFactory providerFactory,
    ISessionStore sessionStore,
    IRequestContextAccessor requestContextAccessor)
    : ICommandHandler<VerifyPasswordlessLoginCommand, VerifyPasswordlessLoginResult>
{
    public async Task<VerifyPasswordlessLoginResult> ExecuteAsync(
        VerifyPasswordlessLoginCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var provider = providerFactory.GetPasswordlessProvider(cmd.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        if (!client.Login.AllowedLoginProviders.HasFlag(provider.LoginProvider))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        using var scope = requestContextAccessor.BeginSystemContext(client.IsolationId);

        var result = await provider.VerifyAsync(
            new PasswordlessVerifyRequest
            {
                ChallengeKey = cmd.InteractionKey,
                Code = cmd.Code
            },
            new ProviderConfig
            {
                ProviderKey = provider.ProviderType,
                ProviderType = provider.ProviderType,
                IsolationId = client.IsolationId,
                IsActive = true
            },
            ct);

        if (!result.Succeeded)
            throw Invalid(nameof(VerifyPasswordlessLoginCommand.Code), MapFailure(result.FailureReason));

        var (session, token) = await sessionStore.CreateAsync(
            principalId: Guid.Parse(result.PrincipalId!),
            clientId: client.ClientId,
            ttl: TimeSpan.FromSeconds(client.Session.RefreshTokenTtlSeconds),
            deviceName: null,
            userAgent: cmd.UserAgent,
            ipAddress: cmd.IpAddress,
            authMethod: provider.ProviderType,
            ct: ct);

        return new VerifyPasswordlessLoginResult
        {
            SessionToken = token,
            ExpiresAt = session.ExpiresAt
        };
    }

    private static string MapFailure(string? reason) => reason switch
    {
        PasswordlessFailures.CodeExpired => IdpMessageKeys.LoginCodeExpired,
        _ => IdpMessageKeys.LoginCodeInvalid
    };

    private static ValidationException Invalid(string property, string messageKey)
        => new([new ValidationFailure(property, messageKey)]);
}

public sealed class VerifyPasswordlessLoginCommand : ICommand<VerifyPasswordlessLoginResult>
{
    public string InteractionKey { get; set; } = default!;

    public string ProviderType { get; set; } = default!;

    public string Code { get; set; } = default!;

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

public sealed class VerifyPasswordlessLoginResult
{
    public required string SessionToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class VerifyPasswordlessLoginValidator : Validator<VerifyPasswordlessLoginCommand>
{
    public VerifyPasswordlessLoginValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ProviderType).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage(ValidationErrors.NotEmptyError)
            .Matches(@"^[0-9]{6}$").WithMessage(IdpMessageKeys.LoginCodeInvalid);
    }
}
