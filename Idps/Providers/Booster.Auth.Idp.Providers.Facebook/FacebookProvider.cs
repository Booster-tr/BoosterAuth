using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.FederationBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using System.Security.Cryptography;
using System.Text;

namespace Booster.Auth.Idp.Providers.Facebook;


public sealed class FacebookProvider(IHttpClientFactory httpClientFactory)
    : OAuth2FederationProviderBase(httpClientFactory)
{
    public const string Type = "facebook";

    public override string ProviderType => Type;

    private const string GraphVersion = "v21.0";

    protected override string AuthorizationEndpoint => $"https://www.facebook.com/{GraphVersion}/dialog/oauth";

    protected override string TokenEndpoint => $"https://graph.facebook.com/{GraphVersion}/oauth/access_token";

    protected override string UserInfoEndpoint => $"https://graph.facebook.com/{GraphVersion}/me?fields=id,name,email";

    protected override string DefaultScopes => "email public_profile";

    protected override string DefaultSubjectField => "id";

    protected override bool TrustEmailVerified => true;

    protected override string ProfileUrl(string accessToken, ProviderConfig config)
    {
        var secret = Setting(config, FederationSettings.ClientSecret);
        if (secret is null)
            return UserInfoEndpoint;

        var proof = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(accessToken)));

        return $"{UserInfoEndpoint}&appsecret_proof={proof}";
    }
}
