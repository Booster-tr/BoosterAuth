namespace Booster.Auth.Idp.Options;

public class ManagementSettings
{
    public string GrpcAddress { get; set; } = default!;

    public int TimeoutSeconds { get; set; } = 5;
}
