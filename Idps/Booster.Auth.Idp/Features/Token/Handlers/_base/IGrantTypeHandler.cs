using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Features.Token.Handlers.Models;
using Booster.Auth.Idp.Stores.ClientStores.Models;

namespace Booster.Auth.Idp.Features.Token.Handlers._base;

public interface IGrantTypeHandler
{
    OAuthGrantType GrantType { get; }

    Task<TokenResponse> HandleAsync(TokenRequest request, ClientData client, CancellationToken ct);
}