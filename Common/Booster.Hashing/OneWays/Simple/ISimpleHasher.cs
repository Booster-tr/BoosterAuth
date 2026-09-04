namespace Booster.Hashing.OneWays.Simple;

public interface ISimpleHasher
{
    string Hash(string value);

    bool Verify(string value, string hash);
}
