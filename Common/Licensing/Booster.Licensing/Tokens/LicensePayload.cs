using System.Text.Json;

namespace Booster.Licensing.Tokens;

/// <summary>
/// Lisans token'inin JSON payload'i (v=1). Alan adlari sabittir.
/// Alan eklemek veya degistirmek yeni bir surum (v=2) demektir; eski urun surumleri yeni bicimi Invalid sayar.
/// </summary>
public sealed record LicensePayload
{
    public const int CurrentVersion = 1;

    public int V { get; init; } = CurrentVersion;
    public required string Kid { get; init; }
    public required Guid LicenseId { get; init; }
    public required LicenseCustomer Customer { get; init; }
    public required string Plan { get; init; }
    /// <summary><see cref="LicenseTypes.Trial"/> veya <see cref="LicenseTypes.Paid"/>.</summary>
    public required string Type { get; init; }
    public required LicenseLimits Limits { get; init; }
    public required IReadOnlyList<string> AllowedDomains { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required int GraceDays { get; init; }
    public required Guid InstallationId { get; init; }

    public bool IsTrial => Type == LicenseTypes.Trial;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed record LicenseCustomer(Guid Id, string Name);

public sealed record LicenseLimits(int Users, int Pools, int Clients);

public static class LicenseTypes
{
    public const string Trial = "trial";
    public const string Paid = "paid";

    public static bool IsKnown(string type) => type is Trial or Paid;
}
