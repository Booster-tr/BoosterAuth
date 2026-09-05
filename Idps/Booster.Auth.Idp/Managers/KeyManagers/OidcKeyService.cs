using Booster.Auth.Idp.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace Booster.Auth.Idp.Managers.KeyManagers;


public class OidcKeyService
{
    private readonly List<RsaSecurityKey> _signingKeys = [];

    public string ActiveKeyId { get; }

    public OidcKeyService(IdpOptions options, IHostEnvironment environment)
    {
        var pem = options.PrivateKeyPem;
        var hasPem = !string.IsNullOrWhiteSpace(pem) && pem.Contains("-----BEGIN");
        var hasPemFile = !string.IsNullOrWhiteSpace(pem) && File.Exists(pem);

        RSA rsa;
        if (hasPem)
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(pem);
        }
        else if (hasPemFile)
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(pem));
        }
        else if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "IdpOptions.PrivateKeyPem is required in production. Refusing to start with an ephemeral signing key.");
        }
        else
        {
            rsa = RSA.Create(2048);
        }

        ActiveKeyId = string.IsNullOrWhiteSpace(options.SigningKeyId)
            ? Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportRSAPublicKey()))[..16]
            : options.SigningKeyId;

        _signingKeys.Add(new RsaSecurityKey(rsa) { KeyId = ActiveKeyId });
    }

    public SigningCredentials GetSigningCredentials()
        => new(_signingKeys[0], SecurityAlgorithms.RsaSha256);

    public IReadOnlyList<SecurityKey> GetValidationKeys()
        => _signingKeys.Cast<SecurityKey>().ToList();

    public IReadOnlyList<JsonWebKey> GetPublicJsonWebKeys()
    {
        var result = new List<JsonWebKey>(_signingKeys.Count);
        foreach (var signingKey in _signingKeys)
        {
            var publicParameters = signingKey.Rsa!.ExportParameters(includePrivateParameters: false);
            var publicKey = new RsaSecurityKey(publicParameters) { KeyId = signingKey.KeyId };
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
            jwk.Use = "sig";
            jwk.Alg = SecurityAlgorithms.RsaSha256;
            result.Add(jwk);
        }
        return result;
    }
}

