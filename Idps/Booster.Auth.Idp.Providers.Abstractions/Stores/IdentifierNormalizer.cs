namespace Booster.Auth.Idp.Providers.Abstractions.Stores;

/// <summary>
/// Giris kimliklerinin tek bicimi. Kayit, arama ve eslestirme ayni kurali kullanir:
/// <list type="bullet">
/// <item>E-posta ve kullanici adi kucuk harf (<c>Ali</c> ile <c>ali</c> ayni hesaptir; once iki ayri hesap acilabiliyordu).</item>
/// <item>Telefon E.164: <c>0555 000 77 88</c> havuzun varsayilan ulke koduyla <c>+905550007788</c> olur
/// (once yalnizca tam E.164 yazilirsa giris yapilabiliyordu).</item>
/// </list>
/// </summary>
public static class IdentifierNormalizer
{
    public static string Email(string value) => value.Trim().ToLowerInvariant();

    public static string UserName(string value) => value.Trim().ToLowerInvariant();

    /// <summary>"+90 532 000 00 00" / "0532..." / "532..." -> "+905320000000". Ulke kodu bulunamazsa null.</summary>
    public static string? Phone(string? value, string? defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        var digits = new string(v.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0 || v.Any(c => char.IsLetter(c) || c == '@'))
            return null;

        string full;
        if (v.StartsWith('+'))
            full = digits;
        else if (v.StartsWith("00", StringComparison.Ordinal))
            full = digits[2..];
        else
        {
            var cc = new string((defaultCountryCode ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            if (cc.Length == 0)
                return null;
            full = cc + digits.TrimStart('0');
        }

        return full.Length is >= 8 and <= 15 ? "+" + full : null;
    }
}
