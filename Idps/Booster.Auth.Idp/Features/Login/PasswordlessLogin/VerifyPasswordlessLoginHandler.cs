using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Managers.LoginChallengeManagers;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
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
    ILocalUserStore userStore,
    ILoginChallengeManager loginChallengeManager,
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

        // Kod birinci adimi kanitladi; oturum havuzun dogrulama / MFA adimlarindan sonra acilir. MFA, kodun gittigi
        // kanal disindaki kanaldan istenir (bkz. LoginChallengeManager.BeginAsync).
        var user = await userStore.GetByIdAsync(Guid.Parse(result.PrincipalId!), ct)
            ?? throw Invalid(nameof(VerifyPasswordlessLoginCommand.Code), IdpMessageKeys.LoginCodeInvalid);

        var outcome = await loginChallengeManager.BeginAsync(
            cmd.InteractionKey, client, user,
            authMethod: provider.ProviderType,
            passwordLogin: true,
            cmd.UserAgent, cmd.IpAddress, ct,
            provenChannel: provider.ProviderType == PhoneCodeType ? ChallengeChannel.Sms : ChallengeChannel.Email);

        return new VerifyPasswordlessLoginResult { SessionToken = outcome.SessionToken, ExpiresAt = outcome.ExpiresAt };
    }

    // Saglayici projesine referans vermemek icin tip adi burada (PhoneCodeProvider.Type).
    private const string PhoneCodeType = "phone_code";

    private static string MapFailure(string? reason) => reason switch
    {
        PasswordlessFailures.CodeExpired => IdpMessageKeys.LoginCodeExpired,
        PasswordlessFailures.AttemptsExceeded => IdpMessageKeys.LoginCodeAttemptsExceeded,
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
    /// <summary>Null ise oturum acilmadi; kullanici dogrulama / MFA adimina yonlendirilir.</summary>
    public string? SessionToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool RequiresChallenge => SessionToken is null;
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
