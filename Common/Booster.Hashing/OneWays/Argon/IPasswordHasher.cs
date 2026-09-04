namespace Booster.Hashing.OneWays.Argon;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
    bool NeedsRehash(string storedHash);
}
