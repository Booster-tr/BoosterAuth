using System.Globalization;

namespace Booster.Auth.Idp.Models;

/// <summary>
/// İzin (consent) ekranının görüntü modeli.
/// <para>
/// Bu tip yalnızca UI katmanına aittir. Bir ConsentController yazıldığında
/// tek yapılması gereken <see cref="Create"/> çağırmaktır:
/// <code>
/// return View(ConsentViewModel.Create(clientKey, client.DisplayName, scope, interactionKey));
/// </code>
/// </para>
/// </summary>
public sealed class ConsentViewModel
{
    public required string InteractionKey { get; init; }

    /// <summary>OAuth <c>client_id</c> (istemci anahtarı).</summary>
    public required string ClientKey { get; init; }

    /// <summary>Kullanıcıya gösterilecek uygulama adı. Boşsa <see cref="ClientKey"/> kullanılır.</summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// Uygulama logosu: <c>IClientLogoStore.Find(..).RelativeUrl</c> (uygulama köküne göreli)
    /// ya da mutlak bir https adresi. Geçersizse null bırakılır.
    /// </summary>
    public string? ClientLogoUri { get; init; }

    /// <summary>Oturumu açık olan kullanıcının görünen adı (varsa).</summary>
    public string? UserDisplayName { get; init; }

    /// <summary>İzin sonrası dönülecek adresin ana bilgisayar adı (varsa).</summary>
    public string? RedirectHost { get; init; }

    /// <summary>Ham <c>scope</c> değeri; forma olduğu gibi geri taşınır.</summary>
    public required string Scope { get; init; }

    public required IReadOnlyList<ConsentScopeViewModel> Scopes { get; init; }

    /// <summary>Logo yoksa gösterilecek monogram.</summary>
    public string Monogram => BuildMonogram(ClientName);

    public static ConsentViewModel Create(
        string clientKey,
        string? clientName,
        string scope,
        string interactionKey,
        string? clientLogoUri = null,
        string? userDisplayName = null,
        string? redirectUri = null)
    {
        var scopes = (scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(Describe)
            .ToList();

        string? host = null;
        if (!string.IsNullOrEmpty(redirectUri) && Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
            host = uri.Host;

        return new ConsentViewModel
        {
            InteractionKey = interactionKey,
            ClientKey = clientKey,
            ClientName = string.IsNullOrWhiteSpace(clientName) ? clientKey : clientName,
            ClientLogoUri = IsSafeLogoUri(clientLogoUri) ? clientLogoUri : null,
            UserDisplayName = userDisplayName,
            RedirectHost = host,
            Scope = scope ?? string.Empty,
            Scopes = scopes
        };
    }

    /// <summary>
    /// Bilinen scope'ların Türkçe karşılıkları. Bilinmeyen bir scope sessizce
    /// gizlenmez — kullanıcı neye izin verdiğini görmelidir — ama ne anlama
    /// geldiğinden emin olunamayacağı açıkça söylenir.
    /// </summary>
    private static ConsentScopeViewModel Describe(string scope) => scope switch
    {
        // Baslik / aciklama kaynak anahtaridir (Consent.Scope.*); gorunum dile gore cevirir.
        "openid" => Known(scope, ConsentScopeIcon.Identity, required: true),
        "profile" => Known(scope, ConsentScopeIcon.User),
        "email" => Known(scope, ConsentScopeIcon.Mail),
        "offline_access" => Known(scope, ConsentScopeIcon.Refresh),
        _ => new(scope, ConsentScopeIcon.Key, Humanize(scope), "Consent.Scope.Unknown.Desc", false, false)
    };

    private static ConsentScopeViewModel Known(string scope, string icon, bool required = false)
        => new(scope, icon, $"Consent.Scope.{scope}.Title", $"Consent.Scope.{scope}.Desc", required, true);

    /// <summary>"reports:read" -&gt; "Reports Read". Türkçe kültürle büyük harfe çevrilir.</summary>
    private static string Humanize(string scope)
    {
        var words = scope
            .Replace('_', ' ').Replace(':', ' ').Replace('.', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var text = string.Join(' ', words.Select(w =>
            string.Concat(culture.TextInfo.ToUpper(w[..1]), w[1..])));

        return text.Length > 40 ? string.Concat(text.AsSpan(0, 40), "…") : text;
    }

    private static string BuildMonogram(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "?";

        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var initials = words.Length == 1
            ? words[0][..1]
            : string.Concat(words[0][..1], words[1][..1]);

        // tr-TR kültürü şart: "istanbul" -> "İ", "I" değil.
        return culture.TextInfo.ToUpper(initials);
    }

    /// <summary>
    /// Kök-göreli bir yol mu ("/client-logos/.."). "//" ve "/\" başka bir
    /// sunucuya işaret eder; yerel sayılmaz.
    /// </summary>
    public static bool IsLocalPath(string value)
        => value.StartsWith('/') && !value.StartsWith("//") && !value.StartsWith("/\\");

    /// <summary>
    /// Yalnızca kök-göreli yollara ve mutlak https adreslerine izin verilir.
    /// Doğrulanamayan adres hiç render edilmez, monogram gösterilir.
    /// </summary>
    private static bool IsSafeLogoUri(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && (IsLocalPath(value)
               || (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps));
}

/// <param name="Name">Ham scope adı (protokol değeri, İngilizce).</param>
/// <param name="Icon">Listede gösterilecek ikon anahtarı.</param>
/// <param name="Title">Tanınan kapsamda kaynak anahtarı (Consent.Scope.*.Title), tanınmayanda okunur ad.</param>
/// <param name="Description">Kaynak anahtarı (Consent.Scope.*.Desc).</param>
/// <param name="Required">Oturum açmak için zorunlu mu?</param>
/// <param name="IsKnown">
/// Tanınan bir scope mu? Tanınmayanlarda ham protokol adı da gösterilir —
/// kullanıcının neye izin verdiğini değerlendirebilmesi için tek ipucu odur.
/// Tanınanlarda gösterilmez: son kullanıcı ekranında <c>openid</c> yazmak
/// geliştirici konsolu alışkanlığıdır, kurumsal bir onay ekranına ait değildir.
/// </param>
public sealed record ConsentScopeViewModel(
    string Name,
    string Icon,
    string Title,
    string Description,
    bool Required,
    bool IsKnown);

/// <summary>İzin listesi ikon anahtarları (Views/Consent/Index.cshtml içinde çizilir).</summary>
public static class ConsentScopeIcon
{
    public const string Identity = "identity";
    public const string User = "user";
    public const string Mail = "mail";
    public const string Phone = "phone";
    public const string Address = "address";
    public const string Refresh = "refresh";
    public const string Api = "api";
    public const string Badge = "badge";
    public const string Key = "key";
}
