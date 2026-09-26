namespace Booster.Hashing.Options;

public sealed class HashingOptions
{
    public string Pepper { get; init; }

    public int MemorySizeKb { get; init; } = 19456;

    public int Iterations { get; init; } = 2;

    public int DegreeOfParallelism { get; init; } = 1;

    public EncryptionOptions EncryptionOptions { get; set; }
}

public class EncryptionOptions
{
    public string ActiveKeyId { get; set; } = default!;
    public Dictionary<string, string> Keys { get; set; } = [];
}
