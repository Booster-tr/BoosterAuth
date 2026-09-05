namespace Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;

public sealed class AuthorizeRedirectException(
    string error, string redirectUri, string? state = null, string? description = null) : Exception(error)
{
    public string Error { get; } = error;
    public string RedirectUri { get; } = redirectUri;
    public string? State { get; } = state;
    public string? Description { get; } = description;
}