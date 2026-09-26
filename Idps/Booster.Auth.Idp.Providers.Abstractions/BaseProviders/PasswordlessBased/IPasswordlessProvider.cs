using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;

public interface IPasswordlessProvider : IIdentityProvider
{
    IdentifierType IdentifierType { get; }

    Task<PasswordlessStartResult> StartAsync(
        PasswordlessStartRequest request,
        ProviderConfig config,
        CancellationToken ct);

    Task<IdentityResult> VerifyAsync(
        PasswordlessVerifyRequest request,
        ProviderConfig config,
        CancellationToken ct);
}
