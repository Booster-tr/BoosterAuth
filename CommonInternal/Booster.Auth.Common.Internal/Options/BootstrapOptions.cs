namespace Booster.Auth.Common.Internal.Options;


public class BootstrapOptions
{
    public string AdminPassword { get; set; } = default!;

    /// <summary>
    /// Ilk kurulumda olusan admin'in e-postasi. 2FA (e-posta kodu) acilacaksa GERCEK bir
    /// posta kutusu olmali; yoksa kod ulasmaz ve admin kilitlenir. Bos: admin@booster.local.
    /// Yalnizca ilk kurulumda kullanilir; mevcut admin'in e-postasini degistirmez.
    /// </summary>
    public string? AdminEmail { get; set; }

    public string ManagementClientRedirectUri { get; set; } = default!;

    public string ManagementClientPostLogoutRedirectUri { get; set; } = default!;

    public string ManagementClientAudience { get; set; } = default!;

    public string IdpAudience { get; set; } = default!;

    public string AuthZAudience { get; set; } = default!;

    /// <summary>Walk to Win mobil uygulamasının ilk kurulum ayarları.</summary>
    public WalkToWinBootstrapOptions WalkToWin { get; set; } = new();

    /// <summary>Lisans portalinin musteri havuzu ve client'i. Varsayilan KAPALI.</summary>
    public LicensingBootstrapOptions Licensing { get; set; } = new();
}

public class LicensingBootstrapOptions
{
    /// <summary>
    /// YALNIZCA Booster'in kendi kurulumunda true. Varsayilan false; boylece musterinin on-prem kurulumunda
    /// bizim musteri havuzumuz ve portal client'imiz olusmaz.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Portal adresi + "/signin-oidc", or. https://licensing.appnuraymind.com/signin-oidc.</summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>Portal adresi + "/signout-callback-oidc".</summary>
    public List<string> PostLogoutRedirectUris { get; set; } = [];
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
