using System.Security.Cryptography;
using System.Text;

namespace Booster.Hashing.OneWays.Simple;

public sealed class SimpleHasher : ISimpleHasher
{
    public string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Base64UrlEncode(bytes);
    }

    public bool Verify(string value, string hash)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(hash))
            return false;

        var computed = Hash(value);

        var a = Encoding.UTF8.GetBytes(computed);
        var b = Encoding.UTF8.GetBytes(hash);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
