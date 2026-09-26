using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Booster.Auth.Idp.Providers.GitHub;

public sealed class GitHubProvider(IHttpClientFactory httpClientFactory)
    : OAuth2FederationProviderBase(httpClientFactory)
{
    public const string Type = "github";

    public override string ProviderType => Type;

    private const string UserAgent = "Booster-Auth";

    protected override string AuthorizationEndpoint => "https://github.com/login/oauth/authorize";

    protected override string TokenEndpoint => "https://github.com/login/oauth/access_token";

    protected override string UserInfoEndpoint => "https://api.github.com/user";

    protected override string DefaultScopes => "read:user user:email";

    protected override string DefaultSubjectField => "id";

    protected override bool UsePkce => false;


    protected override void ConfigureTokenRequest(HttpRequestMessage request, ProviderConfig config)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);
    }

    protected override void ConfigureProfileRequest(HttpRequestMessage request, ProviderConfig config)
        => request.Headers.UserAgent.ParseAdd(UserAgent);

    protected override async Task<(string? Email, bool Verified)> RefineEmailAsync(
        string accessToken, ProviderConfig config, string? email, bool emailVerified, CancellationToken ct)
    {
        var body = await GetJsonAsync("https://api.github.com/user/emails", accessToken, config, ct);

        if (body is null || body.Value.ValueKind != JsonValueKind.Array)
            return (email, emailVerified);

        foreach (var entry in body.Value.EnumerateArray())
        {
            if (!ReadBool(entry, "primary"))
                continue;

            var primary = ReadString(entry, "email");
            if (string.IsNullOrEmpty(primary))
                continue;

            return (primary, ReadBool(entry, "verified"));
        }

        return (email, emailVerified);
    }
}
