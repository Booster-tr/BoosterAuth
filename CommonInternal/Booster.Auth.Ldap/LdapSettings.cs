namespace Booster.Auth.Ldap;

/// <summary>
/// Bir havuzun LDAP / Active Directory baglantisi. Kimlik saglayicisi (IdentityProvider, tur "ldap") ayarlarindan okunur;
/// servis hesabi parolasi sifreli saklanan sirdir (client_secret).
/// </summary>
public sealed record LdapSettings
{
    public const string ProviderType = "ldap";

    // Ayar anahtarlari (IdentityProvider.Settings; panelde ayni anahtarlar)
    public const string KeyHost = "host";
    public const string KeyPort = "port";
    public const string KeySecurity = "security";
    public const string KeyBindDn = "bind_dn";
    public const string KeyBaseDn = "base_dn";
    public const string KeyUserFilter = "user_filter";
    public const string KeyIdAttribute = "id_attribute";
    public const string KeyEmailAttribute = "email_attribute";
    public const string KeyNameAttribute = "name_attribute";
    public const string KeyUsernameAttribute = "username_attribute";
    public const string KeyBindPassword = "client_secret";

    public const string SecurityLdaps = "ldaps";
    public const string SecurityStartTls = "starttls";
    public const string SecurityNone = "none";
    public static readonly string[] SecurityOptions = [SecurityLdaps, SecurityStartTls, SecurityNone];

    /// <summary>Active Directory icin varsayilan filtre; OpenLDAP icin "(uid={0})".</summary>
    public const string DefaultUserFilter = "(&(objectClass=user)(sAMAccountName={0}))";

    public required string Host { get; init; }
    public int Port { get; init; } = 636;
    public string Security { get; init; } = SecurityLdaps;
    public required string BindDn { get; init; }
    public required string BindPassword { get; init; }
    public required string BaseDn { get; init; }
    public string UserFilter { get; init; } = DefaultUserFilter;
    public string IdAttribute { get; init; } = "objectGUID";
    public string EmailAttribute { get; init; } = "mail";
    public string NameAttribute { get; init; } = "displayName";
    public string UsernameAttribute { get; init; } = "sAMAccountName";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Ayar sozlugunden (client_secret dahil) okur. Eksik zorunlu alan varsa null.</summary>
    public static LdapSettings? From(IReadOnlyDictionary<string, string> s)
    {
        string? Get(string k) => s.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        string Or(string k, string d) => Get(k) ?? d;

        var host = Get(KeyHost);
        var bindDn = Get(KeyBindDn);
        var baseDn = Get(KeyBaseDn);
        var password = s.TryGetValue(KeyBindPassword, out var p) ? p : null;
        if (host is null || bindDn is null || baseDn is null || string.IsNullOrEmpty(password))
            return null;

        var security = Or(KeySecurity, SecurityLdaps).ToLowerInvariant();
        var port = int.TryParse(Get(KeyPort), out var pt) ? pt : security == SecurityLdaps ? 636 : 389;

        return new LdapSettings
        {
            Host = host,
            Port = port,
            Security = security,
            BindDn = bindDn,
            BindPassword = password,
            BaseDn = baseDn,
            UserFilter = Or(KeyUserFilter, DefaultUserFilter),
            IdAttribute = Or(KeyIdAttribute, "objectGUID"),
            EmailAttribute = Or(KeyEmailAttribute, "mail"),
            NameAttribute = Or(KeyNameAttribute, "displayName"),
            UsernameAttribute = Or(KeyUsernameAttribute, "sAMAccountName"),
        };
    }

    /// <summary>Panel/sunucu dogrulamasi: hata anahtari ya da null.</summary>
    public static string? Validate(IReadOnlyDictionary<string, string> s)
    {
        string? Get(string k) => s.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        if (Get(KeyPort) is { } port && (!int.TryParse(port, out var n) || n is < 1 or > 65535))
            return "port";
        if (Get(KeySecurity) is { } sec && !SecurityOptions.Contains(sec.ToLowerInvariant()))
            return "security";
        if (Get(KeyUserFilter) is { } f && (!f.Contains("{0}") || !f.StartsWith('(') || !f.EndsWith(')')))
            return "user_filter";
        return null;
    }
}

public sealed record LdapUser(string Subject, string Dn, string? Username, string? Email, string? DisplayName);

public enum LdapFailure
{
    None = 0,
    /// <summary>Kullanici bulunamadi ya da parola yanlis (ayrimi bilerek yapilmaz).</summary>
    InvalidCredentials,
    /// <summary>Sunucuya baglanilamadi / TLS kurulamadi / zaman asimi.</summary>
    Unreachable,
    /// <summary>Servis hesabi bind edemedi ya da arama koku yok: yapilandirma hatasi.</summary>
    Misconfigured,
    /// <summary>Filtre birden cok kayit dondurdu; guvenlik icin giris reddedilir.</summary>
    Ambiguous,
}

public sealed record LdapAuthResult(LdapUser? User, LdapFailure Failure, string? Detail = null)
{
    public bool Succeeded => User is not null;
    public static LdapAuthResult Fail(LdapFailure f, string? detail = null) => new(null, f, detail);
}

public sealed record LdapTestResult(bool Ok, string Code, string? Detail = null);
