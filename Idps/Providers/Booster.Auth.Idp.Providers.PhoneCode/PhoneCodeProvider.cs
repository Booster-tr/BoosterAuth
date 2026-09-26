using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Stores;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;

namespace Booster.Auth.Idp.Providers.PhoneCode;

/// <summary>
/// Telefon numarasına gönderilen tek kullanımlık kodla parolasız giriş.
/// <para>
/// Kod mekaniği <see cref="PasswordlessProviderBase"/>'te; buradaki tek gerçek
/// iş numaranın kayıttaki biçime çevrilmesi.
/// </para>
/// </summary>
public sealed class PhoneCodeProvider(
    IProviderPrincipalStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher)
    : PasswordlessProviderBase(principalStore, codeStore, codeHasher)
{
    /// <summary>
    /// Havuz politikasındaki <see cref="LoginProviderType.PhoneCode"/> ile
    /// eşleşen ad.
    /// </summary>
    public const string Type = "phone_code";

    /// <summary>
    /// Ülke kodu yazılmadığında kullanılacak kodu taşıyan ayar anahtarı
    /// (<see cref="ProviderConfig.Settings"/>).
    /// </summary>
    public const string DefaultCountryCodeSetting = "default_country_code";

    public override string ProviderType => Type;

    public override IdentifierType IdentifierType => IdentifierType.Phone;

    public override LoginProviderType LoginProvider => LoginProviderType.PhoneCode;

    /// <summary>
    /// Numarayı kayıttaki biçime (<c>+&lt;ülke&gt;&lt;numara&gt;</c>) çevirir —
    /// kayıt akışı da numarayı böyle yazıyor (bkz. RegisterHandler.CombinePhone).
    /// <para>
    /// Ülke kodu YOKSA ve ayarda da tanımlı değilse numara REDDEDİLİR, tahmin
    /// edilmez. Varsayılan bir ülke uydurmak, aynı yerel numaranın başka bir
    /// ülkedeki başka bir kullanıcıya ait olması hâlinde kodu YANLIŞ KİŞİYE
    /// göndermek demekti.
    /// </para>
    /// <para>
    /// Tam E.164 ayrıştırma (ülkeye göre uzunluk ve ön ek kuralları) bu sınıfın
    /// işi değil; gerekirse libphonenumber gibi bir kitaplıkla değiştirilebilir.
    /// </para>
    /// </summary>
    protected override string Normalize(string identifier, ProviderConfig config)
    {
        var raw = identifier.Trim();
        if (raw.Length == 0)
            return string.Empty;

        // "+90 532 111 22 33" ve "0090532…" aynı numaranın iki yazımıdır:
        // ikisi de uluslararası biçimdir, ülke kodu zaten içindedir.
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

        // Yerel yazım: baştaki sıfır ülke kodunun yerini tutar, atılır.
        digits = digits.TrimStart('0');
        if (digits.Length == 0)
            return string.Empty;

        var countryCode = ResolveDefaultCountryCode(config);
        if (countryCode is null)
            return string.Empty;

        return $"+{countryCode}{digits}";
    }

    protected override string? ResolveTarget(ProviderPrincipal principal)
        => principal.PhoneNumber;

    private static string? ResolveDefaultCountryCode(ProviderConfig config)
    {
        if (!config.Settings.TryGetValue(DefaultCountryCodeSetting, out var value))
            return null;

        var digits = new string([.. value.Where(char.IsAsciiDigit)]);
        return digits.Length == 0 ? null : digits;
    }
}
