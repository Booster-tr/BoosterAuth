namespace Booster.Authorization.Fga.Options;

public class FgaSettings
{
    public string GrpcAddress { get; set; } = default!;

    public int TimeoutSeconds { get; set; } = 5;

    public bool AllowInsecureHttp2 { get; set; }
}
