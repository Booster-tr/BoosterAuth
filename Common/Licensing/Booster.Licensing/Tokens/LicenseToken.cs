using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Booster.Licensing.Tokens;

/// <summary>
/// Token bicimi: <c>BSTR1.&lt;payload&gt;.&lt;imza&gt;</c>. Iki parca da base64url (dolgusuz).
/// Imza Ed25519'dur ve <c>"BSTR1." + payload</c> metninin ASCII baytlari uzerinden atilir. Boylece onek ve surum
/// de imzanin kapsamina girer; biri oneki degistirirse imza tutmaz.
/// Bu sinif yalnizca cozer ve imzayi dogrular. Sure, GUID ve domain kontrolu <see cref="Validation.LicenseValidator"/>'dadir.
/// </summary>
public static class LicenseToken
{
    public const string Prefix = "BSTR1";
    public const int PublicKeySize = 32;
    public const int SignatureSize = 64;

    public static string SigningInput(string payloadSegment) => $"{Prefix}.{payloadSegment}";

    public static string EncodePayload(LicensePayload payload) =>
        Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload, LicensePayload.JsonOptions));

    public static string Assemble(string payloadSegment, byte[] signature) =>
        $"{SigningInput(payloadSegment)}.{Base64Url.EncodeToString(signature)}";

    public enum ReadResult { Ok, Malformed, UnsupportedVersion, UnknownKid, BadSignature }

    public static ReadResult TryRead(string? token, Func<string, byte[]?> publicKeyByKid, out LicensePayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token)) return ReadResult.Malformed;

        var parts = token.Trim().Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return ReadResult.Malformed;
        if (!TryDecode(parts[1], out var json) || !TryDecode(parts[2], out var signature) || signature.Length != SignatureSize)
            return ReadResult.Malformed;

        LicensePayload? parsed;
        try { parsed = JsonSerializer.Deserialize<LicensePayload>(json, LicensePayload.JsonOptions); }
        catch (JsonException) { return ReadResult.Malformed; }
        if (parsed is null || !LicenseTypes.IsKnown(parsed.Type) || parsed.AllowedDomains is null)
            return ReadResult.Malformed;
        if (parsed.V != LicensePayload.CurrentVersion) return ReadResult.UnsupportedVersion;

        var publicKey = publicKeyByKid(parsed.Kid);
        if (publicKey is null || publicKey.Length != PublicKeySize) return ReadResult.UnknownKid;
        if (!Verify(publicKey, Encoding.ASCII.GetBytes(SigningInput(parts[1])), signature)) return ReadResult.BadSignature;

        payload = parsed;
        return ReadResult.Ok;
    }

    private static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static bool TryDecode(string segment, out byte[] data)
    {
        data = [];
        // Base64Url.IsValid dolgu (=) kabul eder; bicimimiz dolgusuz, bu yuzden ayrica reddedilir.
        if (segment.Length == 0 || segment.Contains('=') || !Base64Url.IsValid(segment)) return false;
        data = Base64Url.DecodeFromChars(segment);
        return true;
    }
}
