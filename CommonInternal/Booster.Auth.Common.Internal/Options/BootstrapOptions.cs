namespace Booster.Auth.Common.Internal.Options;


public class BootstrapOptions
{
    public string AdminPassword { get; set; } = default!;

    public string ManagementClientRedirectUri { get; set; } = default!;

    public string ManagementClientPostLogoutRedirectUri { get; set; } = default!;

    public string ManagementClientAudience { get; set; } = default!;
}
