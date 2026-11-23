using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace Booster.Auth.Ldap;

/// <summary>
/// LDAP / Active Directory islemleri. Akis (standart "search then bind"):
/// servis hesabiyla bind -> kullaniciyi filtreyle ara (tek kayit) -> bulunan DN ve KULLANICININ parolasiyla yeni
/// baglantida bind. Parolalar dizinde kalir; Booster'da saklanmaz.
/// <para>
/// GUVENLIK:
/// - Bos parola KESINLIKLE reddedilir (LDAP'ta bos parolali bind "anonim bind" sayilip basarili donebilir).
/// - Kullanici girdisi filtreye RFC 4515'e gore kacislanarak konur (LDAP injection).
/// - Birden cok kayit eslesirse giris reddedilir.
/// - LDAPS / StartTLS sertifikasi isletim sisteminin guven deposuna gore dogrulanir (kurum CA'si EXTRA_CA_CERTS ile eklenir).
/// </para>
/// </summary>
public sealed class LdapDirectory(ILogger<LdapDirectory> logger)
{
    public async Task<LdapAuthResult> AuthenticateAsync(LdapSettings settings, string identifier, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password))
            return LdapAuthResult.Fail(LdapFailure.InvalidCredentials);

        LdapEntry entry;
        try
        {
            using var service = await OpenAsync(settings, ct);
            await service.BindAsync(settings.BindDn, settings.BindPassword, ct);

            var filter = settings.UserFilter.Replace("{0}", EscapeFilterValue(identifier.Trim()));
            var attrs = new[] { settings.IdAttribute, settings.EmailAttribute, settings.NameAttribute, settings.UsernameAttribute, "cn" };
            var constraints = new LdapSearchConstraints { MaxResults = 2, ServerTimeLimit = (int)settings.Timeout.TotalSeconds, TimeLimit = (int)settings.Timeout.TotalMilliseconds };
            var results = await service.SearchAsync(settings.BaseDn, LdapConnection.ScopeSub, filter, attrs, false, constraints, ct);

            var found = new List<LdapEntry>(2);
            try
            {
                while (await results.HasMoreAsync(ct))
                    found.Add(await results.NextAsync(ct));
            }
            catch (LdapException e) when (e.ResultCode == LdapException.SizeLimitExceeded)
            {
                return LdapAuthResult.Fail(LdapFailure.Ambiguous);
            }

            if (found.Count == 0) return LdapAuthResult.Fail(LdapFailure.InvalidCredentials);
            if (found.Count > 1) return LdapAuthResult.Fail(LdapFailure.Ambiguous);
            entry = found[0];
        }
        catch (Exception e) when (Classify(e) is { } failure)
        {
            logger.LogWarning("LDAP arama basarisiz ({Host}): {Failure} {Code}", settings.Host, failure, (e as LdapException)?.ResultCode);
            return LdapAuthResult.Fail(failure, e.Message);
        }

        // Kullanicinin kendi parolasiyla, AYRI baglantida dogrulama.
        try
        {
            using var user = await OpenAsync(settings, ct);
            await user.BindAsync(entry.Dn, password, ct);
        }
        catch (LdapException e) when (e.ResultCode == LdapException.InvalidCredentials)
        {
            return LdapAuthResult.Fail(LdapFailure.InvalidCredentials);
        }
        catch (Exception e) when (Classify(e) is { } failure)
        {
            logger.LogWarning("LDAP kullanici dogrulamasi basarisiz ({Host}): {Failure}", settings.Host, failure);
            return LdapAuthResult.Fail(failure == LdapFailure.Misconfigured ? LdapFailure.InvalidCredentials : failure, e.Message);
        }

        var subject = ReadId(entry, settings.IdAttribute);
        if (string.IsNullOrEmpty(subject))
            return LdapAuthResult.Fail(LdapFailure.Misconfigured, $"'{settings.IdAttribute}' alani bos");

        return new LdapAuthResult(new LdapUser(
            subject,
            entry.Dn,
            Read(entry, settings.UsernameAttribute) ?? identifier.Trim(),
            Read(entry, settings.EmailAttribute)?.Trim().ToLowerInvariant(),
            Read(entry, settings.NameAttribute) ?? Read(entry, "cn")), LdapFailure.None);
    }

    /// <summary>Panel "Baglantiyi test et": baglan + servis hesabi bind + arama kokunu oku.</summary>
    public async Task<LdapTestResult> TestAsync(LdapSettings settings, CancellationToken ct)
    {
        try
        {
            using var conn = await OpenAsync(settings, ct);
            try { await conn.BindAsync(settings.BindDn, settings.BindPassword, ct); }
            catch (LdapException e) when (e.ResultCode == LdapException.InvalidCredentials)
            { return new LdapTestResult(false, "bind_failed"); }

            try
            {
                var results = await conn.SearchAsync(settings.BaseDn, LdapConnection.ScopeBase, "(objectClass=*)", ["1.1"], false,
                    new LdapSearchConstraints { MaxResults = 1, TimeLimit = (int)settings.Timeout.TotalMilliseconds }, ct);
                while (await results.HasMoreAsync(ct)) await results.NextAsync(ct);
            }
            catch (LdapException e) when (e.ResultCode == LdapException.NoSuchObject)
            { return new LdapTestResult(false, "base_not_found"); }

            return new LdapTestResult(true, "ok");
        }
        catch (Exception e) when (Classify(e) is { } failure)
        {
            return new LdapTestResult(false, failure == LdapFailure.Unreachable ? (IsTls(e) ? "tls_failed" : "unreachable") : "failed", e.Message);
        }
    }

    private static async Task<LdapConnection> OpenAsync(LdapSettings s, CancellationToken ct)
    {
        var conn = new LdapConnection
        {
            SecureSocketLayer = s.Security == LdapSettings.SecurityLdaps,
            ConnectionTimeout = (int)s.Timeout.TotalMilliseconds,
        };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(s.Timeout);
            await conn.ConnectAsync(s.Host, s.Port, timeout.Token);
            if (s.Security == LdapSettings.SecurityStartTls)
                await conn.StartTlsAsync(timeout.Token);
            return conn;
        }
        catch
        {
            conn.Dispose();
            throw;
        }
    }

    private static LdapFailure? Classify(Exception e) => e switch
    {
        OperationCanceledException => LdapFailure.Unreachable,
        SocketException or IOException or AuthenticationException => LdapFailure.Unreachable,
        LdapException l when l.ResultCode is LdapException.ConnectError or LdapException.ServerDown
            or LdapException.LdapTimeout or LdapException.Unavailable => LdapFailure.Unreachable,
        LdapException l when l.ResultCode is LdapException.InvalidCredentials or LdapException.NoSuchObject
            or LdapException.InsufficientAccessRights or LdapException.InvalidDnSyntax or LdapException.FilterError => LdapFailure.Misconfigured,
        LdapException => LdapFailure.Misconfigured,
        _ => null,
    };

    private static bool IsTls(Exception e)
    {
        for (var x = e; x is not null; x = x.InnerException)
            if (x is AuthenticationException) return true;
        return false;
    }

    private static string? Read(LdapEntry entry, string attribute)
    {
        try { return entry.GetAttributeSet().TryGetValue(attribute, out var a) && !string.IsNullOrWhiteSpace(a.StringValue) ? a.StringValue : null; }
        catch (KeyNotFoundException) { return null; }
    }

    /// <summary>objectGUID ikili (16 bayt) gelir; GUID metnine cevrilir. Digerleri (entryUUID vb.) metin olarak.</summary>
    private static string? ReadId(LdapEntry entry, string attribute)
    {
        if (!entry.GetAttributeSet().TryGetValue(attribute, out var a)) return null;
        var bytes = a.ByteValue;
        if (string.Equals(attribute, "objectGUID", StringComparison.OrdinalIgnoreCase) && bytes is { Length: 16 })
            return new Guid(bytes).ToString();
        return string.IsNullOrWhiteSpace(a.StringValue) ? null : a.StringValue.Trim();
    }

    /// <summary>RFC 4515: filtre degerinde * ( ) \ NUL ve ASCII disi karakterler \XX olarak kacislanir.</summary>
    public static string EscapeFilterValue(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (c is '*' or '(' or ')' or '\\' or '\0' || b > 0x7F)
                sb.Append('\\').Append(b.ToString("x2"));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }
}
