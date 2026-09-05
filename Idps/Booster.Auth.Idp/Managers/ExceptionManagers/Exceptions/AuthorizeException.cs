namespace Booster.Auth.Idp.Managers.ExceptionManagers.Exceptions;

public sealed class AuthorizeException(string errorKey, string? description = null) : Exception(errorKey)
{
    public string ErrorKey { get; } = errorKey;
    public string? Description { get; } = description;
}
