namespace Booster.Auth.Idp.Options;

public class IdpOptions
{
    public string Issuer { get; set; } = default!;

    public string SigningKeyId { get; set; } = string.Empty;

    public string PrivateKeyPem { get; set; } = string.Empty;

    public string ApplicationPeper { get; set; } = string.Empty;
}