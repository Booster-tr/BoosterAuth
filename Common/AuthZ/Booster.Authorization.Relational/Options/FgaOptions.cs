namespace Booster.Authorization.Relational.Options;

public class FgaOptions
{
    public string ApiUrl { get; set; } = default!;
    public string ApiToken { get; set; } = default!;

    /// <summary>Bir şema sürümündeki en fazla kaynak tipi.</summary>
    public int MaxSchemaTypes { get; set; } = 10;

    /// <summary>Bir kaynak tipindeki en fazla yetki (bağlantılar hariç).</summary>
    public int MaxPermissionsPerType { get; set; } = 10;

    /// <summary>Bir havuzdaki en fazla şema sürümü.</summary>
    public int MaxSchemaVersions { get; set; } = 5;
}
