namespace Booster.Hashing.TwoWays.Aes;

public interface ISecretProtector
{
    string Protect(string plaintext);

    string? Unprotect(string protectedValue);
}
