namespace Booster.Auth.Idp.Stores.InteractionKeyStores.Models;

//Login öncesi
//public sealed class InteractionStateData
//{
//    public required string ClientKey { get; init; }
//    public required Guid ClientId { get; init; }
//    public required Guid IsolationId { get; init; }
//    public required string RedirectUri { get; init; }
//    public string ResponseType => "code";
//    public required string Scope { get; init; }
//    public string? State { get; init; }
//    public string? Nonce { get; init; }
//    public required string CodeChallenge { get; init; }
//    public string CodeChallengeMethod => "S256";
//    public string? Resource { get; init; }
//    public string? UiLocale { get; init; }
//    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
//}

public sealed class InteractionStateData
{
    public required string ClientKey { get; init; }
    public required string RedirectUri { get; init; }
    public string ResponseType => "code";
    public required string Scope { get; init; }
    public string? State { get; init; }
    public string? Nonce { get; init; }
    public required string CodeChallenge { get; init; }
    public string CodeChallengeMethod => "S256";
    public string? Resource { get; init; }
    public string? UiLocale { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

//Login sonrası
public sealed class AuthorizationCodeData
{
    public required Guid ClientId { get; init; }
    public required Guid PrincipalId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid IsolationId { get; init; }
    public required string Audience { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public string? Nonce { get; init; }
    public string? CodeChallenge { get; init; }
    public string? CodeChallengeMethod { get; init; }
}