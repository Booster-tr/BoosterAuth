using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationProviders;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
public abstract class FederationProviderBase(IHttpClientFactory httpClientFactory) : IFederationProvider
{
    public abstract string ProviderType { get; }

    public ProviderKind Kind => ProviderKind.Federation;

    public LoginProviderType LoginProvider => LoginProviderType.Federation;

    public abstract Task<FederationChallenge> BuildChallengeAsync(
        FederationContext context, ProviderConfig config, CancellationToken ct);

    public abstract Task<FederationResult> HandleCallbackAsync(
        FederationCallback callback, ProviderConfig config, CancellationToken ct);


    protected abstract string DefaultScopes { get; }

    protected HttpClient CreateClient() => httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "federation";

    protected static string? Setting(ProviderConfig config, string name)
        => config.Settings.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    protected string Scopes(ProviderConfig config)
        => Setting(config, FederationSettings.Scopes) ?? DefaultScopes;

    protected static string BuildUrl(string endpoint, IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join('&', parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

        var separator = endpoint.Contains('?') ? '&' : '?';

        return $"{endpoint}{separator}{query}";
    }

    protected async Task<JsonElement?> ExchangeCodeAsync(
        string tokenEndpoint,
        string code,
        FederationCallback callback,
        ProviderConfig config,
        CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = callback.RedirectUri,
            ["client_id"] = Setting(config, FederationSettings.ClientId) ?? string.Empty
        };

        var secret = await ResolveClientSecretAsync(config, ct);
        if (!string.IsNullOrEmpty(secret))
            form["client_secret"] = secret;

        if (!string.IsNullOrEmpty(callback.CodeVerifier))
            form["code_verifier"] = callback.CodeVerifier;

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };

        ConfigureTokenRequest(request, config);

        using var client = CreateClient();
        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadJsonAsync(response, ct);
    }

    protected virtual Task<string?> ResolveClientSecretAsync(ProviderConfig config, CancellationToken ct)
        => Task.FromResult(Setting(config, FederationSettings.ClientSecret));

    protected virtual void ConfigureTokenRequest(HttpRequestMessage request, ProviderConfig config) { }

    protected static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            return document?.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    protected static string? ReadString(JsonElement element, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return null;

        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null
        };
    }

    protected static bool ReadBool(JsonElement element, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;

        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(property.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    protected static string? PreflightFailure(FederationCallback callback, ProviderConfig config)
    {
        if (!config.IsActive)
            return FederationFailures.ProviderInactive;

        if (callback.Error is not null)
            return FederationFailures.AuthorizationDenied;

        if (callback.Code is null)
            return FederationFailures.CodeMissing;

        return null;
    }
}
