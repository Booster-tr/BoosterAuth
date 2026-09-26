using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.Stores;
using Booster.Auth.Idp.Providers.Abstractions.Stores.Models;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;

namespace Booster.Auth.Idp.Providers.PhoneCode;

public sealed class PhoneCodeProvider(
    ILocalUserStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher)
    : PasswordlessProviderBase(principalStore, codeStore, codeHasher)
{

    public const string Type = "phone_code";

    public const string DefaultCountryCodeSetting = "default_country_code";

    public override string ProviderType => Type;

    public override IdentifierType IdentifierType => IdentifierType.Phone;

    public override LoginProviderType LoginProvider => LoginProviderType.PhoneCode;

    protected override string Normalize(string identifier, ProviderConfig config)
    {
        var raw = identifier.Trim();
        if (raw.Length == 0)
            return string.Empty;

        var international = raw.StartsWith('+') || raw.StartsWith("00", StringComparison.Ordinal);

        var digits = new string([.. raw.Where(char.IsAsciiDigit)]);
        if (digits.Length == 0)
            return string.Empty;

        if (international)
        {
            if (digits.StartsWith("00", StringComparison.Ordinal))
                digits = digits[2..];

            return digits.Length == 0 ? string.Empty : $"+{digits}";
        }

        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            return string.Empty;

        var countryCode = ResolveDefaultCountryCode(config);
        if (countryCode is null)
            return string.Empty;

        return $"+{countryCode}{digits}";
    }

    protected override string? ResolveTarget(LocalUser principal)
        => principal.PhoneNumber;

    private static string? ResolveDefaultCountryCode(ProviderConfig config)
    {
        if (!config.Settings.TryGetValue(DefaultCountryCodeSetting, out var value))
            return null;

        var digits = new string([.. value.Where(char.IsAsciiDigit)]);
        return digits.Length == 0 ? null : digits;
    }
}
