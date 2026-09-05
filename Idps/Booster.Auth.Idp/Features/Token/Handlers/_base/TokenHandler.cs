using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Constants;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Stores.ClientStores;
using Booster.ExceptionManager.Abstraction.Exceptions;

namespace Booster.Auth.Idp.Features.Token.Handlers._base;

public sealed class TokenHandler(
    IClientDataStore clientDataStore,
    IEnumerable<IGrantTypeHandler> grantHandlers)
{
    public async Task<TokenResponse> ExecuteAsync(TokenRequest request, CancellationToken ct)
    {
        var grantType = MapGrantType(request.GrantType.Trim());
        if (grantType == OAuthGrantType.None)
            throw new BusinessException(IdpMessageKeys.UnsupportedGrantType);

        var handler = grantHandlers.FirstOrDefault(h => h.GrantType == grantType)
            ?? throw new BusinessException(IdpMessageKeys.UnsupportedGrantType);

        if (string.IsNullOrEmpty(request.ClientKey))
            throw new BusinessException(IdpMessageKeys.ClientNotFound);

        var client = await clientDataStore.GetByClientKeyAsync(request.ClientKey, ct)
            ?? throw new BusinessException(IdpMessageKeys.ClientNotFound);

        if (!client.IsActive)
            throw new BusinessException(IdpMessageKeys.ClientInactive);

        if (!client.AllowedGrantTypes.HasFlag(grantType))
            throw new BusinessException(IdpMessageKeys.UnsupportedGrantType);

        if (!string.IsNullOrEmpty(request.Resource) &&
            !client.AllowedAudiences.Any(a => string.Equals(a.Trim(), request.Resource.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(IdpMessageKeys.InvalidResource);
        }

        return await handler.HandleAsync(request, client, ct);
    }

    private static OAuthGrantType MapGrantType(string grantType) => grantType switch
    {
        "authorization_code" => OAuthGrantType.AuthorizationCode,
        "client_credentials" => OAuthGrantType.ClientCredentials,
        "refresh_token" => OAuthGrantType.RefreshToken,
        "urn:ietf:params:oauth:grant-type:token-exchange" => OAuthGrantType.TokenExchange,
        _ => OAuthGrantType.None
    };
}
