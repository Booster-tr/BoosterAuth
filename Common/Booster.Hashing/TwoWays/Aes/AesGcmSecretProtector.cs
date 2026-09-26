using Booster.Hashing.Options;
using System.Security.Cryptography;
using System.Text;

namespace Booster.Hashing.TwoWays.Aes;

public sealed class AesGcmSecretProtector : ISecretProtector
{
    private const string Version = "v1";

    private const int NonceSize = 12;

    private const int TagSize = 16;

    private const int KeySize = 32;

    private readonly string _activeKeyId;
    private readonly IReadOnlyDictionary<string, byte[]> _keys;

    public AesGcmSecretProtector(EncryptionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ActiveKeyId))
            throw new InvalidOperationException("EncryptionOptions.ActiveKeyId değeri boş olamaz.");

        if (options.Keys.Count == 0)
            throw new InvalidOperationException("EncryptionOptions.Keys boş olamaz.");

        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var (id, base64) in options.Keys)
        {
            var key = Convert.FromBase64String(base64);

            if (key.Length != KeySize)
                throw new InvalidOperationException($"EncryptionOptions.Keys['{id}'] {KeySize} bayt olmalıdır.");

            keys[id] = key;
        }

        if (!keys.ContainsKey(options.ActiveKeyId))
            throw new InvalidOperationException("EncryptionOptions.ActiveKeyId, Keys içinde bulunamadı.");

        _activeKeyId = options.ActiveKeyId;
        _keys = keys;
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var key = _keys[_activeKeyId];

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        return string.Join(':',
            Version,
            _activeKeyId,
            Convert.ToBase64String(nonce),
            Convert.ToBase64String(cipher),
            Convert.ToBase64String(tag));
    }

    public string? Unprotect(string protectedValue)
    {
        if (string.IsNullOrEmpty(protectedValue))
            return null;

        var parts = protectedValue.Split(':');

        if (parts.Length != 5 || parts[0] != Version)
            return null;

        if (!_keys.TryGetValue(parts[1], out var key))
            return null;

        try
        {
            var nonce = Convert.FromBase64String(parts[2]);
            var cipher = Convert.FromBase64String(parts[3]);
            var tag = Convert.FromBase64String(parts[4]);

            if (nonce.Length != NonceSize || tag.Length != TagSize)
                return null;

            var plainBytes = new byte[cipher.Length];

            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, cipher, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception e) when (e is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
