using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Database.Models;

public sealed record ClientData
{
    public required Guid ClientId { get; init; }

    public required string ClientKey { get; init; }
    public required OAuthGrantType AllowedGrantTypes { get; init; }
    public required IReadOnlyList<string> AllowedScopes { get; init; }

    public required Guid IsolationId { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyList<string> RedirectUris { get; init; }
    public required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
    public required IReadOnlyList<string> AllowedAudiences { get; init; }
    public required bool RequireConsent { get; init; }

    public required LoginPolicyData Login { get; init; }
    public required RegistrationPolicyData Registration { get; init; }
    public required PasswordPolicyData Password { get; init; }
    public required SessionPolicyData Session { get; init; }
    public required IReadOnlyList<string> DefaultScopes { get; init; }

    public required IReadOnlyList<DefaultRoleData> DefaultRoles { get; init; }

    public IReadOnlyList<FederationProviderData> FederationProviders { get; init; } = [];

    // required DEGIL: eski bir Management surumu alani gondermezse MFA kapali
    // sayilir. Onbellekteki eski kayitlar ise anahtar surumuyle ayrilir
    // (bkz. ClientDataCache.Key).
    public MfaPolicyData Mfa { get; init; } = new();
}

public sealed record MfaPolicyData
{
    public bool Required { get; init; }

    public MfaMethod AllowedMethods { get; init; } = MfaMethod.None;
}

public sealed record DefaultRoleData
{
    public required Guid RoleId { get; init; }
    public required string RoleName { get; init; }
}
public sealed record LoginPolicyData
{
    public required LoginProviderType AllowedLoginProviders { get; init; }

    public required IReadOnlyList<IdentifierType> AllowedIdentifiers { get; init; }

    public required int MaxFailedAttempts { get; init; }
    public required int LockoutSeconds { get; init; }

    public string? DefaultCountryCode { get; init; }
}

public sealed record RegistrationPolicyData
{
    public required RegistrationMode Mode { get; init; }
    public required bool RequireEmailVerification { get; init; }
    public required bool RequirePhoneVerification { get; init; }
    public required bool AllowFederatedJit { get; init; }
}

public sealed record PasswordPolicyData
{
    public required int MinLength { get; init; }
    public required bool RequireUppercase { get; init; }
    public required bool RequireLowercase { get; init; }
    public required bool RequireDigit { get; init; }
    public required bool RequireNonAlphanumeric { get; init; }

    public required int HistoryCount { get; init; }

    public required int ExpiryDays { get; init; }
}

public sealed record SessionPolicyData
{
    public required int AccessTokenTtlSeconds { get; init; }
    public required int RefreshTokenTtlSeconds { get; init; }
    public required int IdleTimeoutSeconds { get; init; }
}
public sealed record FederationProviderData
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required string ProviderType { get; init; }

    public required string DisplayName { get; init; }
}
