using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Managers.CookieManagers;
using Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;
using Booster.Auth.Idp.Options;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.Auth.Idp.Stores.InteractionKeyStores.Models;
using Booster.Auth.Idp.Stores.SessionStores;
using Booster.Caching.Abstraction;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Constants;
using FastEndpoints;
using FluentValidation;
using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.Auth.Idp.Managers.LicenseManagers;

namespace Booster.Auth.Idp.Features.Authorize;

public class AuthorizeEndpoint(
    IClientDataStore clientDataStore,
    ISessionStore sessionStore,
    IAsyncRepository<Consent, IdpDbContext> consentRepository,
    ISecureCacheStore<InteractionStateData> interactionStore,
    ISecureCacheStore<AuthorizationCodeData> codeStore,
    ISessionCookieManager cookieManager,
    IdpUiOptions uiOptions,
    EndUserLicenseGate licenseGate) : Endpoint<AuthorizeRequest>
{
    private static readonly TimeSpan InteractionTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(2);

    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("authorize");
        AllowAnonymous();
        Summary(s => s.Summary = "OIDC authorization endpoint (yetkilendirme kodu üretir)");
    }

    public override async Task HandleAsync(AuthorizeRequest req, CancellationToken ct)
    {
        var (interactionKey, interaction) = await EnsureInteractionAsync(req, ct);

        var client = await ResolveAndValidateClientAsync(interaction, ct);

        ValidateProtocol(interaction, client);

        if (IsRegisterPrompt(req))
        {
            await RedirectAsync(uiOptions.GetRegisterUrl(interactionKey));
            return;
        }

        var session = IsLoginPrompt(req) ? null : await ResolveSessionAsync(client, ct);

        if (session is null)
        {
            await RedirectAsync(uiOptions.GetLoginUrl(interactionKey));
            return;
        }

        if (client.RequireConsent &&
            !await HasValidConsentAsync(session.PrincipalId, client.ClientId, interaction.Scope, ct))
        {
            await RedirectAsync(uiOptions.GetConsentUrl(client.ClientKey, interaction.Scope, interactionKey));
            return;
        }

        await IssueCodeAndRedirectAsync(interaction, interactionKey, client, session, ct);
    }

    private async Task<(string key, InteractionStateData interaction)> EnsureInteractionAsync(
        AuthorizeRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(req.InteractionKey))
        {
            var existing = await interactionStore.GetAsync(req.InteractionKey, ct)
                ?? throw new AuthorizeException(IdpMessageKeys.InvalidInteraction);
            return (req.InteractionKey, existing);
        }

        var interaction = new InteractionStateData
        {
            ClientKey = req.ClientKey!,
            RedirectUri = req.RedirectUri!,
            Scope = req.Scope!,
            State = req.State,
            Nonce = req.Nonce,
            CodeChallenge = req.CodeChallenge!,
            Resource = req.Resource
        };

        var key = await interactionStore.StoreAsync(interaction, InteractionTtl, ct);
        return (key, interaction);
    }

    private async Task<ClientData> ResolveAndValidateClientAsync(InteractionStateData interaction, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(interaction.ClientKey))
            throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        var client = await clientDataStore.GetByClientKeyAsync(interaction.ClientKey, ct)
            ?? throw new AuthorizeException(IdpMessageKeys.ClientNotFound);

        if (!client.IsActive)
            throw new AuthorizeException(IdpMessageKeys.ClientInactive);

        // Yalnizca deneme lisansinin ek suresi bittiyse; son kullaniciya teknik ayrinti gosterilmez.
        if (!await licenseGate.IsAllowedAsync(client, ct))
            throw new AuthorizeException(LicenseMessageKeys.ServiceUnavailable);

        if (string.IsNullOrEmpty(interaction.RedirectUri) || !client.RedirectUris.Contains(interaction.RedirectUri, StringComparer.Ordinal))
            throw new AuthorizeException(IdpMessageKeys.InvalidRedirectUri);

        if (!client.AllowedGrantTypes.HasFlag(OAuthGrantType.AuthorizationCode))
            throw new AuthorizeException(IdpMessageKeys.CodeFlowNotSupported);

        return client;
    }

    private static void ValidateProtocol(InteractionStateData interaction, ClientData client)
    {
        if (string.IsNullOrEmpty(interaction.CodeChallenge))
            throw new AuthorizeRedirectException("invalid_request", interaction.RedirectUri, interaction.State,
                "code_challenge zorunludur");

        if (string.IsNullOrWhiteSpace(interaction.Scope))
            throw new AuthorizeRedirectException("invalid_scope", interaction.RedirectUri, interaction.State,
                "scope zorunludur");

        var requested = interaction.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!requested.All(s => OidcScopes.IsStandard(s) || client.AllowedScopes.Contains(s, StringComparer.Ordinal)))
            throw new AuthorizeRedirectException("invalid_scope", interaction.RedirectUri, interaction.State,
                "istenen scope istemci için izinli değil");
    }

    private async Task<Session?> ResolveSessionAsync(ClientData client, CancellationToken ct)
    {
        var token = cookieManager.Get(HttpContext.Request);
        if (string.IsNullOrEmpty(token))
            return null;

        var session = await sessionStore.ResolveAsync(token, ct);

        if (session is null || session.IsolationId != client.IsolationId)
            return null;

        return session;
    }

    private async Task<bool> HasValidConsentAsync(Guid principalId, Guid clientId, string requestedScope, CancellationToken ct)
    {
        var consent = await consentRepository.GetAsync(
            predicate: c => c.PrincipalId == principalId && c.ClientId == clientId && c.DeletedTime == null,
            enableTracking: false,
            withoutGlobalFilter: false,
            cancellationToken: ct);

        if (consent is null)
            return false;

        var now = DateTimeOffset.UtcNow;
        if (consent.RevokedAt is not null || (consent.ExpiresAt is not null && consent.ExpiresAt <= now))
            return false;

        var requested = requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var granted = consent.GrantedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return requested.All(s => granted.Contains(s, StringComparer.OrdinalIgnoreCase));
    }

    private async Task IssueCodeAndRedirectAsync(
        InteractionStateData interaction, string interactionKey, ClientData client, Session session, CancellationToken ct)
    {
        var codeData = new AuthorizationCodeData
        {
            ClientId = client.ClientId,
            PrincipalId = session.PrincipalId,
            SessionId = session.Id,
            IsolationId = client.IsolationId,
            Audience = interaction.Resource ?? string.Empty,
            RedirectUri = interaction.RedirectUri,
            Scope = interaction.Scope,
            Nonce = interaction.Nonce,
            CodeChallenge = interaction.CodeChallenge,
            CodeChallengeMethod = "S256"
        };

        var code = await codeStore.StoreAsync(codeData, CodeTtl, ct);
        await interactionStore.RemoveAsync(interactionKey, ct);

        var builder = new UriBuilder(interaction.RedirectUri);
        var query = $"code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrEmpty(interaction.State))
            query += $"&state={Uri.EscapeDataString(interaction.State)}";
        builder.Query = query;

        await RedirectAsync(builder.ToString());
    }

    private Task RedirectAsync(string url)
        => Send.RedirectAsync(url, isPermanent: false, allowRemoteRedirects: true);

    private static bool IsRegisterPrompt(AuthorizeRequest req)
        => string.Equals(req.Prompt, "create", StringComparison.OrdinalIgnoreCase)
        || string.Equals(req.Prompt, "register", StringComparison.OrdinalIgnoreCase);

    private static bool IsLoginPrompt(AuthorizeRequest req)
        => string.Equals(req.Prompt, "login", StringComparison.OrdinalIgnoreCase);
}

public record AuthorizeRequest
{
    [BindFrom("client_id")] public string? ClientKey { get; init; }
    [BindFrom("redirect_uri")] public string? RedirectUri { get; init; }
    [BindFrom("response_type")] public string? ResponseType { get; init; }
    [BindFrom("scope")] public string? Scope { get; init; }
    [BindFrom("state")] public string? State { get; init; }
    [BindFrom("nonce")] public string? Nonce { get; init; }
    [BindFrom("code_challenge")] public string? CodeChallenge { get; init; }
    [BindFrom("code_challenge_method")] public string? CodeChallengeMethod { get; init; }
    [BindFrom("resource")] public string? Resource { get; init; }
    [BindFrom("interaction_key")] public string? InteractionKey { get; init; }
    [BindFrom("prompt")] public string? Prompt { get; init; }
}

public class AuthorizeValidator : Validator<AuthorizeRequest>
{
    public AuthorizeValidator()
    {
        When(x => string.IsNullOrEmpty(x.InteractionKey), () =>
        {
            RuleFor(x => x.ClientKey).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            RuleFor(x => x.RedirectUri).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            RuleFor(x => x.ResponseType).NotEmpty().Equal("code", StringComparer.OrdinalIgnoreCase).WithMessage(IdpMessageKeys.UnsupportedResponseType);
            RuleFor(x => x.Scope).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            RuleFor(x => x.CodeChallenge).NotEmpty().WithMessage(ValidationErrors.NotEmptyError);
            RuleFor(x => x.CodeChallengeMethod).NotEmpty().Equal("S256", StringComparer.OrdinalIgnoreCase).WithMessage(IdpMessageKeys.UnsupportedCodeChallengeType);
        });
    }
}