using Booster.Auth.Database.Models;
using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Management.Grpc;

public static class ClientDataMapper
{
    public static ClientDataMessage ToMessage(ClientData source)
    {
        var message = new ClientDataMessage
        {
            ClientId = source.ClientId.ToString(),
            ClientKey = source.ClientKey,
            AllowedGrantTypes = (int)source.AllowedGrantTypes,
            IsolationId = source.IsolationId.ToString(),
            IsActive = source.IsActive,
            RequireConsent = source.RequireConsent,
            Login = new LoginPolicyMessage
            {
                AllowedLoginProviders = (int)source.Login.AllowedLoginProviders,
                MaxFailedAttempts = source.Login.MaxFailedAttempts,
                LockoutSeconds = source.Login.LockoutSeconds,
                DefaultCountryCode = source.Login.DefaultCountryCode ?? string.Empty
            },
            Registration = new RegistrationPolicyMessage
            {
                Mode = (int)source.Registration.Mode,
                RequireEmailVerification = source.Registration.RequireEmailVerification,
                RequirePhoneVerification = source.Registration.RequirePhoneVerification,
                AllowFederatedJit = source.Registration.AllowFederatedJit
            },
            Password = new PasswordPolicyMessage
            {
                MinLength = source.Password.MinLength,
                RequireUppercase = source.Password.RequireUppercase,
                RequireLowercase = source.Password.RequireLowercase,
                RequireDigit = source.Password.RequireDigit,
                RequireNonAlphanumeric = source.Password.RequireNonAlphanumeric,
                HistoryCount = source.Password.HistoryCount,
                ExpiryDays = source.Password.ExpiryDays
            },
            Session = new SessionPolicyMessage
            {
                AccessTokenTtlSeconds = source.Session.AccessTokenTtlSeconds,
                RefreshTokenTtlSeconds = source.Session.RefreshTokenTtlSeconds,
                IdleTimeoutSeconds = source.Session.IdleTimeoutSeconds
            }
        };

        message.AllowedScopes.AddRange(source.AllowedScopes);
        message.RedirectUris.AddRange(source.RedirectUris);
        message.PostLogoutRedirectUris.AddRange(source.PostLogoutRedirectUris);
        message.AllowedAudiences.AddRange(source.AllowedAudiences);
        message.DefaultScopes.AddRange(source.DefaultScopes);

        message.Login.AllowedIdentifiers.AddRange(source.Login.AllowedIdentifiers.Select(i => (int)i));

        message.DefaultRoles.AddRange(source.DefaultRoles.Select(r => new DefaultRoleMessage
        {
            RoleId = r.RoleId.ToString(),
            RoleName = r.RoleName
        }));

        message.FederationProviders.AddRange(source.FederationProviders.Select(f => new FederationProviderMessage
        {
            Id = f.Id.ToString(),
            Key = f.Key,
            ProviderType = f.ProviderType,
            DisplayName = f.DisplayName
        }));

        return message;
    }

    public static ClientData ToModel(ClientDataMessage message)
        => new()
        {
            ClientId = Guid.Parse(message.ClientId),
            ClientKey = message.ClientKey,
            AllowedGrantTypes = (OAuthGrantType)message.AllowedGrantTypes,
            AllowedScopes = [.. message.AllowedScopes],
            IsolationId = Guid.Parse(message.IsolationId),
            IsActive = message.IsActive,
            RedirectUris = [.. message.RedirectUris],
            PostLogoutRedirectUris = [.. message.PostLogoutRedirectUris],
            AllowedAudiences = [.. message.AllowedAudiences],
            RequireConsent = message.RequireConsent,
            DefaultScopes = [.. message.DefaultScopes],
            Login = new LoginPolicyData
            {
                AllowedLoginProviders = (LoginProviderType)message.Login.AllowedLoginProviders,
                AllowedIdentifiers = [.. message.Login.AllowedIdentifiers.Select(i => (IdentifierType)i)],
                MaxFailedAttempts = message.Login.MaxFailedAttempts,
                LockoutSeconds = message.Login.LockoutSeconds,
                DefaultCountryCode = string.IsNullOrWhiteSpace(message.Login.DefaultCountryCode)
                    ? null
                    : message.Login.DefaultCountryCode
            },
            Registration = new RegistrationPolicyData
            {
                Mode = (RegistrationMode)message.Registration.Mode,
                RequireEmailVerification = message.Registration.RequireEmailVerification,
                RequirePhoneVerification = message.Registration.RequirePhoneVerification,
                AllowFederatedJit = message.Registration.AllowFederatedJit
            },
            Password = new PasswordPolicyData
            {
                MinLength = message.Password.MinLength,
                RequireUppercase = message.Password.RequireUppercase,
                RequireLowercase = message.Password.RequireLowercase,
                RequireDigit = message.Password.RequireDigit,
                RequireNonAlphanumeric = message.Password.RequireNonAlphanumeric,
                HistoryCount = message.Password.HistoryCount,
                ExpiryDays = message.Password.ExpiryDays
            },
            Session = new SessionPolicyData
            {
                AccessTokenTtlSeconds = message.Session.AccessTokenTtlSeconds,
                RefreshTokenTtlSeconds = message.Session.RefreshTokenTtlSeconds,
                IdleTimeoutSeconds = message.Session.IdleTimeoutSeconds
            },
            DefaultRoles = [.. message.DefaultRoles.Select(r => new DefaultRoleData
            {
                RoleId = Guid.Parse(r.RoleId),
                RoleName = r.RoleName
            })],
            FederationProviders = [.. message.FederationProviders.Select(f => new FederationProviderData
            {
                Id = Guid.Parse(f.Id),
                Key = f.Key,
                ProviderType = f.ProviderType,
                DisplayName = f.DisplayName
            })]
        };
}
