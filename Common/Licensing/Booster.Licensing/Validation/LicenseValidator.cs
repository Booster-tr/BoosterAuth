using Booster.Licensing.Tokens;

namespace Booster.Licensing.Validation;

public interface ILicenseValidator
{
    /// <param name="token">Saklanan veya yeni yapistirilan token; bos ise sonuc <see cref="LicenseState.None"/>.</param>
    /// <param name="installationId">Bu kurulumun GUID'i (veritabanindan).</param>
    /// <param name="now">Etkin zaman. Saat geri alma korumasi icin <see cref="Clock.LicenseClock"/>'tan gelmeli.</param>
    LicenseValidationResult Validate(string? token, Guid installationId, DateTimeOffset now);
}

/// <summary>Tamamen cevrimdisi dogrulama: bicim, surum, kid, imza, GUID ve sure. Ag erisimi YOK.</summary>
public sealed class LicenseValidator : ILicenseValidator
{
    private readonly IReadOnlyDictionary<string, byte[]> _publicKeys;

    /// <summary>Urunde kullanilan yol: yalnizca koda gomulu anahtarlar.</summary>
    public LicenseValidator() : this(LicensePublicKeys.All) { }

    /// <summary>Lisans portali kendi urettigi token'i kendi public key'iyle kontrol etmek icin kullanir.</summary>
    public LicenseValidator(IReadOnlyDictionary<string, byte[]> publicKeys) => _publicKeys = publicKeys;

    public LicenseValidationResult Validate(string? token, Guid installationId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token)) return LicenseValidationResult.None(now);

        var read = LicenseToken.TryRead(token, kid => _publicKeys.GetValueOrDefault(kid), out var payload);
        if (read != LicenseToken.ReadResult.Ok)
            return LicenseValidationResult.Invalid(read switch
            {
                LicenseToken.ReadResult.UnsupportedVersion => LicenseInvalidReason.UnsupportedVersion,
                LicenseToken.ReadResult.UnknownKid => LicenseInvalidReason.UnknownKid,
                LicenseToken.ReadResult.BadSignature => LicenseInvalidReason.BadSignature,
                _ => LicenseInvalidReason.Malformed,
            }, now);

        if (payload!.InstallationId != installationId)
            return LicenseValidationResult.Invalid(LicenseInvalidReason.InstallationMismatch, now);

        return new LicenseValidationResult(LicenseStateCalculator.Calculate(payload, now), payload, null, now);
    }
}

public static class LicenseStateCalculator
{
    public const int WarningDays = 14;

    /// <summary>Active -> Warning (bitise 14 gun) -> Grace (bitisten sonra graceDays) -> Restricted.</summary>
    public static LicenseState Calculate(LicensePayload license, DateTimeOffset now)
    {
        if (now < license.ExpiresAt.AddDays(-WarningDays)) return LicenseState.Active;
        if (now < license.ExpiresAt) return LicenseState.Warning;
        if (now < license.ExpiresAt.AddDays(Math.Max(0, license.GraceDays))) return LicenseState.Grace;
        return LicenseState.Restricted;
    }
}
