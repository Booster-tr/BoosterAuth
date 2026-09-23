using System.Text.RegularExpressions;

namespace Booster.Auth.Common.Internal.Helpers;

/// <summary>
/// Zaman aşımı uygulanmış Regex sarmalayıcı.
/// <para>
/// Düz <see cref="Regex"/> yerine bunun kullanılma sebebi: desen ya da girdi
/// dışarıdan geliyorsa (şablon metni, kullanıcı verisi) geri izlemeli eşleşme
/// üstel süreye çıkabilir ve tek bir istek işlem çekirdeğini kilitler (ReDoS).
/// Zaman aşımı bunu isteğin kendisiyle sınırlar.
/// </para>
/// </summary>
public static class SafeRegex
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    public static string Replace(string input, string pattern, MatchEvaluator evaluator)
    {
        try
        {
            return Regex.Replace(input, pattern, evaluator, RegexOptions.None, Timeout);
        }
        catch (RegexMatchTimeoutException)
        {
            // Doldurulamayan metin, isteği çökertmekten iyidir.
            return input;
        }
    }

    public static bool IsMatch(string input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.None, Timeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
