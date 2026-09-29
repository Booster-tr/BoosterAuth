namespace Booster.Auth.Management.Api.Options;

public class IdpSettings
{
    public string GrpcAddress { get; set; } = default!;

    public int TimeoutSeconds { get; set; } = 5;
}
