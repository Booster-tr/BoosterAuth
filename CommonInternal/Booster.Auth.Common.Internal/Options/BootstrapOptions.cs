namespace Booster.Auth.Common.Internal.Options;


public class BootstrapOptions
{
    public string AdminPassword { get; set; } = default!;

    public string ManagementClientRedirectUri { get; set; } = default!;

    public string ManagementClientPostLogoutRedirectUri { get; set; } = default!;

    public string ManagementClientAudience { get; set; } = default!;

    /// <summary>
    /// IdP'nin KENDI API'sinin audience'i (/me, /password/change).
    /// <para>
    /// Istemcinin izinli audience listesinde olmak zorunda: aksi halde
    /// uygulama IdP uclarini cagirabilecek bir token isteyemez, token ucu
    /// invalid_resource dondurur.
    /// </para>
    /// </summary>
    public string IdpAudience { get; set; } = default!;
}
