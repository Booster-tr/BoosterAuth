using System.Text.Json.Serialization;

namespace Booster.Auth.Idp.Managers.LoginChallengeManagers;

/// <summary>Birincil kimlik doğrulamadan sonra, oturum açılmadan önce tamamlanması gereken adım.</summary>
public enum LoginStep
{
    /// <summary>Havuz e-posta doğrulaması istiyor, kullanıcının e-postası doğrulanmamış.</summary>
    VerifyEmail = 1,

    /// <summary>Havuz telefon doğrulaması istiyor, kullanıcının telefonu doğrulanmamış.</summary>
    VerifyPhone = 2,

    /// <summary>Havuz iki adımlı doğrulama istiyor (e-posta ya da SMS kodu).</summary>
    Mfa = 3
}

/// <summary>Kodun gönderildiği kanal.</summary>
public enum ChallengeChannel
{
    Email = 1,
    Sms = 2
}

/// <summary>
/// Yarım kalmış giriş. Etkileşim anahtarıyla saklanır; parola doğru girildi ama
/// oturum henüz AÇILMADI — adımlar bitmeden kullanıcı hiçbir şeye erişemez.
/// </summary>
public sealed record LoginChallengeData
{
    public required Guid PrincipalId { get; init; }

    public required Guid ClientId { get; init; }

    public required string AuthMethod { get; init; }

    public string? UserAgent { get; init; }

    public string? IpAddress { get; init; }

    /// <summary>Kalan adımlar, sırasıyla. İlki şu an ekranda olandır.</summary>
    public required IReadOnlyList<LoginStep> Steps { get; init; }

    /// <summary>MFA için kullanılabilecek kanallar (havuz izni ∩ kullanıcının hedefleri).</summary>
    public IReadOnlyList<ChallengeChannel> MfaChannels { get; init; } = [];

    /// <summary>MFA adımında seçili kanal.</summary>
    public ChallengeChannel? MfaChannel { get; init; }

    /// <summary>Tamamlanan adımlardan MFA var mı — oturumun AuthMethod'una işlenir.</summary>
    public bool MfaCompleted { get; init; }

    [JsonIgnore] public LoginStep CurrentStep => Steps[0];

    [JsonIgnore] public ChallengeChannel CurrentChannel => CurrentStep switch
    {
        LoginStep.VerifyEmail => ChallengeChannel.Email,
        LoginStep.VerifyPhone => ChallengeChannel.Sms,
        _ => MfaChannel ?? MfaChannels[0]
    };
}

/// <summary>Giriş denemesinin sonucu: ya oturum açıldı ya da bir adım bekleniyor.</summary>
public sealed record LoginOutcome
{
    public string? SessionToken { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public bool RequiresChallenge => SessionToken is null;

    public static LoginOutcome Session(string token, DateTimeOffset expiresAt)
        => new() { SessionToken = token, ExpiresAt = expiresAt };

    public static LoginOutcome Challenge() => new();
}

/// <summary>Doğrulama ekranının ihtiyaç duyduğu, maskelenmiş bilgi.</summary>
public sealed record LoginChallengeView(
    LoginStep Step,
    ChallengeChannel Channel,
    string MaskedTarget,
    IReadOnlyList<ChallengeChannel> MfaChannels);
