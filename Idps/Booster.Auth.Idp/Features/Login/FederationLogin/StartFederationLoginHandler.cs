using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Providers.Abstractions;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.FederationStores;
using Booster.Auth.Idp.Stores.FederationStores.Models;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Caching.Abstraction;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using System.Security.Cryptography;
using System.Text;

namespace Booster.Auth.Idp.Features.Login.FederationLogin;

public sealed class StartFederationLoginHandler(
    ISecureCacheStore<InteractionStateData> interactionStore,
    ISecureCacheStore<FederationStateData> stateStore,
    IClientDataStore clientDataStore,
    IFederationProviderConfigStore providerConfigStore,
    IProviderFactory providerFactory,
    IdpUiOptions uiOptions) : ICommandHandler<StartFederationLoginCommand, StartFederationLoginResult>
{
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    public async Task<StartFederationLoginResult> ExecuteAsync(
        StartFederationLoginCommand cmd, CancellationToken ct)
    {
        var interaction = await interactionStore.GetAsync(cmd.InteractionKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        if (!client.Login.AllowedLoginProviders.HasFlag(LoginProviderType.Federation))
            throw new AuthorizeException(IdpMessageKeys.ProviderNotAllowed);

        var config = await providerConfigStore.GetAsync(client.IsolationId, cmd.ProviderKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.FederationProviderNotFound);

        if (!config.IsActive)
            throw new AuthorizeException(IdpMessageKeys.FederationProviderNotFound);

        var provider = providerFactory.GetFederationProvider(config.ProviderType)
            ?? throw new AuthorizeException(IdpMessageKeys.ProviderNotAvailable);

        var redirectUri = uiOptions.GetExternalCallbackUrl();

        var nonce = RandomToken();
        var codeVerifier = RandomToken();

        var state = await stateStore.StoreAsync(
            new FederationStateData
            {
                InteractionKey = cmd.InteractionKey,
                ClientKey = interaction.ClientKey,
                IsolationId = client.IsolationId,
                IdentityProviderId = config.Id,
                ProviderKey = config.Key,
                CodeVerifier = codeVerifier,
                Nonce = nonce,
                RedirectUri = redirectUri
            },
            StateLifetime,
            ct);

        var challenge = await provider.BuildChallengeAsync(
            new FederationContext
            {
                RedirectUri = redirectUri,
                State = state,
                Nonce = nonce,
                CodeChallenge = Challenge(codeVerifier),
                LoginHint = cmd.LoginHint
            },
            config.ToProviderConfig(client.IsolationId),
            ct);

        return new StartFederationLoginResult { RedirectUrl = challenge.RedirectUrl };
    }

    private static string RandomToken()
        => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class StartFederationLoginCommand : ICommand<StartFederationLoginResult>
{
    public string InteractionKey { get; set; } = default!;

    public string ProviderKey { get; set; } = default!;

    public string? LoginHint { get; set; }
}

public sealed class StartFederationLoginResult
{
    public required string RedirectUrl { get; init; }
}

public sealed class StartFederationLoginValidator : Validator<StartFederationLoginCommand>
{
    public StartFederationLoginValidator()
    {
        RuleFor(x => x.InteractionKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
        RuleFor(x => x.ProviderKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError).MaximumLength(100).WithMessage(ValidationErrors.MaxLengthError);
    }
}
