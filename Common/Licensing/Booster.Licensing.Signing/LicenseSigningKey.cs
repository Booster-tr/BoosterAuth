using System.Buffers.Text;
using System.Text;
using System.Text.RegularExpressions;
using Booster.Licensing.Tokens;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace Booster.Licensing.Signing;

/// <summary>
/// Ed25519 imzalama anahtari. Yalnizca lisans portalinda bulunur.
/// Private key disariya yalnizca <see cref="ExportPrivateKey"/> ile cikar (portal veritabanina yazmak icin).
/// ToString anahtari ICERMEZ; boylece yanlislikla log'a dusemez.
/// </summary>
public sealed partial class LicenseSigningKey
{
    private readonly Ed25519PrivateKeyParameters _key;

    private LicenseSigningKey(string kid, Ed25519PrivateKeyParameters key)
    {
        Kid = kid;
        _key = key;
        PublicKey = key.GeneratePublicKey().GetEncoded();
    }

    public string Kid { get; }
    public byte[] PublicKey { get; }
    /// <summary>Urune (LicensePublicKeys) gomulecek deger.</summary>
    public string PublicKeyBase64Url => Base64Url.EncodeToString(PublicKey);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,31}$")]
    private static partial Regex KidRegex();

    public static LicenseSigningKey Generate(string kid)
    {
        EnsureKid(kid);
        return new LicenseSigningKey(kid, new Ed25519PrivateKeyParameters(new SecureRandom()));
    }

    /// <summary>Saklanan 32 baytlik private key'den (seed) yukler.</summary>
    public static LicenseSigningKey FromPrivateKey(string kid, byte[] privateKey)
    {
        EnsureKid(kid);
        if (privateKey.Length != Ed25519PrivateKeyParameters.KeySize)
            throw new ArgumentException("Ed25519 private key 32 bayt olmali.", nameof(privateKey));
        return new LicenseSigningKey(kid, new Ed25519PrivateKeyParameters(privateKey, 0));
    }

    /// <summary>32 baytlik private key (seed). YALNIZCA portal veritabanina yazilir; loglanmaz, ekrana basilmaz.</summary>
    public byte[] ExportPrivateKey() => _key.GetEncoded();

    /// <summary>Payload'i imzalar ve <c>BSTR1.&lt;payload&gt;.&lt;imza&gt;</c> token'ini dondurur.</summary>
    public string Issue(LicensePayload payload)
    {
        if (!string.Equals(payload.Kid, Kid, StringComparison.Ordinal))
            throw new InvalidOperationException($"Payload kid'i ({payload.Kid}) anahtarin kid'i ({Kid}) ile ayni olmali.");

        var segment = LicenseToken.EncodePayload(payload);
        var message = Encoding.ASCII.GetBytes(LicenseToken.SigningInput(segment));
        var signer = new Ed25519Signer();
        signer.Init(true, _key);
        signer.BlockUpdate(message, 0, message.Length);
        return LicenseToken.Assemble(segment, signer.GenerateSignature());
    }

    public override string ToString() => $"LicenseSigningKey(kid={Kid})";

    private static void EnsureKid(string kid)
    {
        if (!KidRegex().IsMatch(kid))
            throw new ArgumentException("kid 2-32 karakter olmali; kucuk harf, rakam veya tire (or. k2026a).", nameof(kid));
    }
}
