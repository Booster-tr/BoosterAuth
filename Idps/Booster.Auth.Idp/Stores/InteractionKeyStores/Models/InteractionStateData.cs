namespace Booster.Auth.Idp.Stores.InteractionKeyStores.Models;

//Login öncesi
//public sealed class InteractionStateData
//{
//    public required string ClientKey { get; init; }
//    public required Guid ClientId { get; init; }
//    public required Guid IsolationId { get; init; }
//    public required string RedirectUri { get; init; }
//    public string ResponseType => "code";
//    public required string Scope { get; init; }
//    public string? State { get; init; }
//    public string? Nonce { get; init; }
//    public required string CodeChallenge { get; init; }
//    public string CodeChallengeMethod => "S256";
//    public string? Resource { get; init; }
//    public string? UiLocale { get; init; }
//    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
//}

public sealed class InteractionStateData
{
    public required string ClientKey { get; init; }
    public required string RedirectUri { get; init; }
    public string ResponseType => "code";
    public required string Scope { get; init; }
    public string? State { get; init; }
    public string? Nonce { get; init; }
    public required string CodeChallenge { get; init; }
    public string CodeChallengeMethod => "S256";
    public string? Resource { get; init; }
    public string? UiLocale { get; init; }

    /// <summary>
    /// Doluysa bu bir OIDC istegi degil, "Hesabim" sayfasinin girisidir: giris tamamlaninca kod uretilmez,
    /// kullanici bu (IdP icindeki) adrese dondurulur. Yalnizca AccountController doldurur.
    /// </summary>
    public string? AccountReturnUrl { get; init; }

    /// <summary>
    /// /authorize'daki <c>hidden_providers</c>: giris ekraninda gosterilmeyecek yontemler. Saglayici tipi ("apple"),
    /// saglayici anahtari ya da "email_code" / "phone_code". Yalnizca ekrani daraltir; yetki kurali degildir.
    /// </summary>
    public List<string> HiddenProviders { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsHidden(params string?[] names)
        => names.Any(n => n is not null && HiddenProviders.Contains(n, StringComparer.OrdinalIgnoreCase));

    /// <summary>Virgul ya da bosluk ayrilmis liste; en fazla 20 deger.</summary>
    public static List<string> ParseProviderList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => v.Length <= 100)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();
}

//Login sonrası
public sealed class AuthorizationCodeData
{
    public required Guid ClientId { get; init; }
    public required Guid PrincipalId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid IsolationId { get; init; }
    public required string Audience { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public string? Nonce { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
}