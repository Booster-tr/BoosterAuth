using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Booster.Auth.Idp.Providers.X;

public sealed class XProvider(IHttpClientFactory httpClientFactory)
    : OAuth2FederationProviderBase(httpClientFactory)
{
    public const string Type = "x";

    public override string ProviderType => Type;

    protected override string AuthorizationEndpoint => "https://x.com/i/oauth2/authorize";

    protected override string TokenEndpoint => "https://api.x.com/2/oauth2/token";

    protected override string UserInfoEndpoint => "https://api.x.com/2/users/me";

    protected override string DefaultScopes => "users.read tweet.read";

    protected override string DefaultSubjectField => "id";

    protected override string DefaultNameField => "name";


    protected override bool UsePkce => true;


    protected override void ConfigureTokenRequest(HttpRequestMessage request, ProviderConfig config)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var clientId = Setting(config, FederationSettings.ClientId);
        var secret = Setting(config, FederationSettings.ClientSecret);

        if (clientId is null || secret is null)
            return;

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    protected override JsonElement? UnwrapProfile(JsonElement root)
        => root.TryGetProperty("data", out var data) ? data : null;
}
