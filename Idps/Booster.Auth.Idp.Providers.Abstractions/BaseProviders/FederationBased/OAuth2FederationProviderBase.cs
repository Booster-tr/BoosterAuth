using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;

public abstract class OAuth2FederationProviderBase(IHttpClientFactory httpClientFactory)
    : FederationProviderBase(httpClientFactory)
{
    protected abstract string AuthorizationEndpoint { get; }
    protected abstract string TokenEndpoint { get; }
    protected abstract string UserInfoEndpoint { get; }

    protected abstract string DefaultSubjectField { get; }

    protected virtual string DefaultEmailField => "email";
    protected virtual string DefaultNameField => "name";

    protected virtual string? DefaultEmailVerifiedField => null;

    protected virtual bool TrustEmailVerified => false;

    protected virtual bool UsePkce => true;

    protected virtual void ExtendAuthorizationParameters(
        Dictionary<string, string?> parameters, FederationContext context, ProviderConfig config) { }

    public override Task<FederationChallenge> BuildChallengeAsync(
        FederationContext context, ProviderConfig config, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = Setting(config, FederationSettings.ClientId),
            ["redirect_uri"] = context.RedirectUri,
            ["scope"] = Scopes(config),
            ["state"] = context.State
        };

        if (UsePkce && !string.IsNullOrEmpty(context.CodeChallenge))
        {
            parameters["code_challenge"] = context.CodeChallenge;
            parameters["code_challenge_method"] = "S256";
        }

        ExtendAuthorizationParameters(parameters, context, config);

        return Task.FromResult(new FederationChallenge
        {
            RedirectUrl = BuildUrl(AuthorizationEndpoint, parameters),
            State = context.State
        });
    }

    public override async Task<FederationResult> HandleCallbackAsync(
        FederationCallback callback, ProviderConfig config, CancellationToken ct)
    {
        if (PreflightFailure(callback, config) is { } failure)
            return FederationResult.Fail(failure);

        var token = await ExchangeCodeAsync(TokenEndpoint, callback.Code!, callback, config, ct);
        if (token is null)
            return FederationResult.Fail(FederationFailures.TokenExchangeFailed);

        var accessToken = ReadString(token.Value, "access_token");
        if (string.IsNullOrEmpty(accessToken))
            return FederationResult.Fail(FederationFailures.TokenExchangeFailed);

        var body = await GetJsonAsync(ProfileUrl(accessToken, config), accessToken, config, ct);
        if (body is null)
            return FederationResult.Fail(FederationFailures.ProfileFetchFailed);


        var profile = UnwrapProfile(body.Value);
        if (profile is null)
            return FederationResult.Fail(FederationFailures.ProfileFetchFailed);

        var subject = ReadString(profile.Value, Setting(config, FederationSettings.SubjectClaim) ?? DefaultSubjectField);
        if (string.IsNullOrEmpty(subject))
            return FederationResult.Fail(FederationFailures.SubjectMissing);

        var email = ReadString(profile.Value, Setting(config, FederationSettings.EmailClaim) ?? DefaultEmailField);
        var name = ReadString(profile.Value, Setting(config, FederationSettings.NameClaim) ?? DefaultNameField);

        var verifiedField = Setting(config, FederationSettings.EmailVerifiedClaim) ?? DefaultEmailVerifiedField;

        var emailVerified = verifiedField is not null
            ? ReadBool(profile.Value, verifiedField)
            : !string.IsNullOrEmpty(email) && TrustEmailVerified;


        (email, emailVerified) = await RefineEmailAsync(accessToken, config, email, emailVerified, ct);

        return FederationResult.Success(new ExternalIdentity
        {
            Subject = subject,
            Email = email,
            EmailVerified = emailVerified,
            DisplayName = name
        });
    }


    protected virtual string ProfileUrl(string accessToken, ProviderConfig config) => UserInfoEndpoint;


    protected virtual JsonElement? UnwrapProfile(JsonElement root) => root;

    protected virtual Task<(string? Email, bool Verified)> RefineEmailAsync(
        string accessToken, ProviderConfig config, string? email, bool emailVerified, CancellationToken ct)
        => Task.FromResult((email, emailVerified));

    protected async Task<JsonElement?> GetJsonAsync(
        string url, string accessToken, ProviderConfig config, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        ConfigureProfileRequest(request, config);

        using var client = CreateClient();
        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadJsonAsync(response, ct);
    }

    protected virtual void ConfigureProfileRequest(HttpRequestMessage request, ProviderConfig config) { }
}
