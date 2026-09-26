namespace Booster.Auth.Idp.Models;

public sealed class ExternalConflictModel
{
    public required string InteractionKey { get; init; }

    public string? Email { get; init; }

    public required string LoginUrl { get; init; }

    public required string ForgotPasswordUrl { get; init; }
}
