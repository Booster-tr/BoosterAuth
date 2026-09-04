namespace Booster.Auth.Manager.RequestContexts;


public sealed class RequestContext
{
    public Guid PrincipalId { get; private init; }
    public string PrincipalType { get; private init; } = "user";
    public Guid? SessionId { get; private init; }
    public string ClientId { get; private init; } = string.Empty;
    public string AccessToken { get; private init; } = string.Empty;
    public string AcceptLanguage { get; private init; } = "tr-tr";

    public Guid Isolation { get; private init; }

    public static readonly RequestContext Empty = ForSystem();

    public static RequestContext ForPrincipal(
        Guid principalId,
        string principalType,
        Guid? sessionId,
        string clientId,
        string accessToken,
        string acceptLanguage,
        Guid isolation)
        => new()
        {
            PrincipalId = principalId,
            PrincipalType = principalType,
            SessionId = sessionId,
            ClientId = clientId,
            AccessToken = accessToken,
            AcceptLanguage = string.IsNullOrWhiteSpace(acceptLanguage) ? "tr-tr" : acceptLanguage,
            Isolation = isolation
        };

    public RequestContext WithIsolation(Guid isolation)
    => new()
    {
        PrincipalId = PrincipalId,
        PrincipalType = PrincipalType,
        SessionId = SessionId,
        ClientId = ClientId,
        AccessToken = AccessToken,
        AcceptLanguage = AcceptLanguage,
        Isolation = isolation
    };

    public static RequestContext ForSystem(
        Guid isolation = default)
        => new()
        {
            PrincipalId = Guid.Empty,
            PrincipalType = "serviceaccount",
            SessionId = null,
            ClientId = "system",
            AccessToken = string.Empty,
            AcceptLanguage = "tr-tr",
            Isolation = isolation
        };
}

