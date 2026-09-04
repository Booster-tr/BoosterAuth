namespace Booster.Hashing.Options;

public sealed class HashingOptions
{
    public required string Pepper { get; init; }

    public int MemorySizeKb { get; init; } = 19456;

    public int Iterations { get; init; } = 2;

    public int DegreeOfParallelism { get; init; } = 1;
}
