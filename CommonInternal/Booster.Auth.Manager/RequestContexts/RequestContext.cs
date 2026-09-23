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

    /// <summary>
    /// Kimliği doğrulanmamış istekler ve arka plan akışları için bağlam.
    /// <para>
    /// Dil parametre olarak alınır: anonim uçlar (dökümantasyon, giriş ve hata
    /// ekranları) da Accept-Language'e uymalı. Sabit "tr-tr" yazılsaydı bu uçlar
    /// istek ne derse desin daima Türkçe dönerdi.
    /// </para>
    /// </summary>
    public static RequestContext ForSystem(
        Guid isolation = default,
        string? acceptLanguage = null)
        => new()
        {
            PrincipalId = Guid.Empty,
            PrincipalType = "serviceaccount",
            SessionId = null,
            ClientId = "system",
            AccessToken = string.Empty,
            AcceptLanguage = string.IsNullOrWhiteSpace(acceptLanguage) ? "tr-tr" : acceptLanguage,
            Isolation = isolation
        };
}

