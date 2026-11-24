using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Booster.Observability;

/// <summary>
/// Disari gonderilen loglardan kisisel ve gizli veriyi temizler (KVKK: veri minimizasyonu; log sistemi kisisel
/// veri deposu olmamali). Mesaj metnine ve degiskenlere uygulanir:
/// <list type="bullet">
/// <item>E-posta: <c>ali@firma.com</c> -&gt; <c>a***@firma.com</c> (alan adi teshis icin kalir).</item>
/// <item>Telefon (E.164): <c>+905321234567</c> -&gt; <c>+90*******567</c>.</item>
/// <item>JWT ve <c>password= / secret= / token= / code=</c> gibi anahtar-deger ciftleri: tamamen gizlenir.</item>
/// <item>Adi parola, gizli anahtar, token, kod iceren degiskenler: deger tamamen gizlenir.</item>
/// </list>
/// Kod bu kurala guvenip kisisel veri loglamamali; bu, gozden kacan icin son savunma hattidir.
/// </summary>
public sealed partial class SensitiveDataRedactor : BaseProcessor<LogRecord>
{
    private const string Hidden = "[gizli]";

    [GeneratedRegex(@"(?<local>[A-Za-z0-9._%+-])[A-Za-z0-9._%+-]*@(?<domain>[A-Za-z0-9.-]+\.[A-Za-z]{2,})")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+(?<cc>\d{2})\d{5,9}(?<tail>\d{3})\b")]
    private static partial Regex Phone();

    [GeneratedRegex(@"eyJ[\w-]{5,}\.[\w-]{5,}\.[\w-]{5,}")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)\b(?<key>password|passwd|secret|client_secret|access_token|refresh_token|id_token|subject_token|token|code|otp|pin|api_key)(?<sep>[=:]\s*)(?<val>[^&\s,;""']+)")]
    private static partial Regex KeyValue();

    [GeneratedRegex(@"(?i)(password|passwd|secret|token|otp|pin|apikey|api_key|authorization|cookie|code)$")]
    private static partial Regex SensitiveKey();

    public override void OnEnd(LogRecord data)
    {
        if (data.FormattedMessage is { } message)
            data.FormattedMessage = Redact(message);
        if (data.Body is { } body)
            data.Body = Redact(body);

        if (data.Attributes is { Count: > 0 } attributes)
        {
            var copy = new List<KeyValuePair<string, object?>>(attributes.Count);
            foreach (var (key, value) in attributes)
            {
                if (key == "{OriginalFormat}")
                    copy.Add(new(key, value));
                else if (SensitiveKey().IsMatch(key))
                    copy.Add(new(key, Hidden));
                else
                    copy.Add(new(key, value is string s ? Redact(s) : value));
            }
            data.Attributes = copy;
        }
    }

    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        value = Jwt().Replace(value, Hidden);
        value = KeyValue().Replace(value, m => $"{m.Groups["key"].Value}{m.Groups["sep"].Value}{Hidden}");
        value = Email().Replace(value, m => $"{m.Groups["local"].Value}***@{m.Groups["domain"].Value}");
        value = Phone().Replace(value, m => $"+{m.Groups["cc"].Value}*******{m.Groups["tail"].Value}");
        return value;
    }
}
