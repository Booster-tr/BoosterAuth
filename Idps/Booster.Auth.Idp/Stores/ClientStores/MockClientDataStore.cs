using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Stores.ClientStores.Models;

namespace Booster.Auth.Idp.Stores.ClientStores;

public sealed class MockClientDataStore : IClientDataStore
{
    private static readonly Guid SampleIsolation = Guid.Parse("08df067a-de4f-1333-30f6-ef36db3f0000");

    public Task<ClientData?> GetByClientKeyAsync(string clientKey, CancellationToken ct)
    {
        if (clientKey != "booster-panel")
            return Task.FromResult<ClientData?>(null);

        var data = new ClientData
        {
            ClientId = Guid.Parse("08deea5c-db95-71e4-30f6-ef36db3f0000"),
            ClientKey = clientKey,
            IsolationId = SampleIsolation,
            IsActive = true,
            RedirectUris = ["http://localhost:5088/callback"],
            PostLogoutRedirectUris = ["http://localhost:5088/"],
            AllowedAudiences = ["http://localhost:5088/authn"],
            RequireConsent = false,
            AllowedGrantTypes = OAuthGrantType.AuthorizationCode | OAuthGrantType.RefreshToken | OAuthGrantType.TokenExchange | OAuthGrantType.ClientCredentials,
            AllowedScopes = ["openid", "profile", "email", "offline_access", "api", "std_user"],
            DefaultScopes = ["openid", "profile"], 
            DefaultRoles =
            [
                new DefaultRoleData { RoleId = Guid.Parse("08df0100-0000-0000-0000-000000000001"), RoleName = "user" }
            ],
            Login = new LoginPolicyData
            {
                AllowedCredentialProviders = ["local", "ldap", "federation"],
                AllowedIdentifiers = [IdentifierType.Email, IdentifierType.Username],
                AllowFederatedLogin = true,
                MaxFailedAttempts = 5,
                LockoutSeconds = 900
            },
            Registration = new RegistrationPolicyData
            {
                Mode = RegistrationMode.SelfSignup,
                RequireEmailVerification = true,
                RequirePhoneVerification = false,
                AllowFederatedJit = true
            },
            Password = new PasswordPolicyData
            {
                MinLength = 8,
                RequireUppercase = true,
                RequireLowercase = true,
                RequireDigit = true,
                RequireNonAlphanumeric = false
            },
            Session = new SessionPolicyData
            {
                AccessTokenTtlSeconds = 900,
                RefreshTokenTtlSeconds = 1_209_600,
                IdleTimeoutSeconds = 0
            }
        };

        return Task.FromResult<ClientData?>(data);
    }
}
