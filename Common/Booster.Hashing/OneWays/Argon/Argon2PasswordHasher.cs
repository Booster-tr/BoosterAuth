using Booster.Hashing.Options;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Booster.Hashing.OneWays.Argon;

public sealed class Argon2PasswordHasher(HashingOptions options) : IPasswordHasher
{
    private const string Prefix = "alperwashere";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);

        return string.Join('$',
            Prefix,
            $"m={options.MemorySizeKb},t={options.Iterations},p={options.DegreeOfParallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
            return false;

        var paramMap = ParseParameters(parts[1]);
        if (paramMap is null)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, paramMap.Value.mem, paramMap.Value.iter, paramMap.Value.par, expected.Length);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private byte[] Derive(string password, byte[] salt)
        => Derive(password, salt, options.MemorySizeKb, options.Iterations, options.DegreeOfParallelism, HashSize);

    private byte[] Derive(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int hashSize)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password + options.Pepper);

        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(hashSize);
    }

    public bool NeedsRehash(string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return true;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix)
            return true;

        var paramMap = ParseParameters(parts[1]);
        if (paramMap is null)
            return true;

        return paramMap.Value.mem < options.MemorySizeKb
            || paramMap.Value.iter < options.Iterations
            || paramMap.Value.par < options.DegreeOfParallelism;
    }

    private static (int mem, int iter, int par)? ParseParameters(string segment)
    {
        int mem = 0, iter = 0, par = 0;
        foreach (var pair in segment.Split(','))
        {
            var kv = pair.Split('=');
            if (kv.Length != 2 || !int.TryParse(kv[1], out var value))
                return null;

            switch (kv[0])
            {
                case "m": mem = value; break;
                case "t": iter = value; break;
                case "p": par = value; break;
            }
        }

        return (mem == 0 || iter == 0 || par == 0) ? null : (mem, iter, par);
    }
}
