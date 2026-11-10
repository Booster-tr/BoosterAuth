namespace Booster.Auth.Common.Internal.Options;


public class BootstrapOptions
{
    public string AdminPassword { get; set; } = default!;

    public string ManagementClientRedirectUri { get; set; } = default!;

    public string ManagementClientPostLogoutRedirectUri { get; set; } = default!;

    public string ManagementClientAudience { get; set; } = default!;

    public string IdpAudience { get; set; } = default!;

    public string AuthZAudience { get; set; } = default!;

    /// <summary>Walk to Win mobil uygulamasının ilk kurulum ayarları.</summary>
    public WalkToWinBootstrapOptions WalkToWin { get; set; } = new();
}

public class WalkToWinBootstrapOptions
{
    /// <summary>Bu ürünü barındırmayan ortamlarda bootstrapper'ı kapatmak için.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Mobil uygulamanın yönlendirme adresleri. Native build: "walktowin://auth/callback".
    /// Geliştirmede Expo Go / web adresleri (exp://..., http://localhost:8081/auth/callback) eklenebilir.
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];

    public List<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>Walk to Win API'si için access token audience değeri (mobil `resource` parametresi).</summary>
    public string ApiAudience { get; set; } = default!;
}
