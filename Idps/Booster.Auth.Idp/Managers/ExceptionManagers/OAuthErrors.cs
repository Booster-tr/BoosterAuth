using Booster.ExceptionManager.Abstraction.Exceptions;
using FluentValidation;

namespace Booster.Auth.Idp.Managers.ExceptionManagers;

/// <summary>
/// Token ucunun mesaj anahtarlarini RFC 6749 5.2 / RFC 8693 / RFC 8707 hata kodlarina esler.
/// </summary>
public static class OAuthErrors
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidClient = "invalid_client";
    public const string InvalidGrant = "invalid_grant";
    public const string InvalidScope = "invalid_scope";
    public const string InvalidTarget = "invalid_target";
    public const string UnsupportedGrantType = "unsupported_grant_type";

    public static string ForToken(Exception exception) => exception switch
    {
        ValidationException => InvalidRequest,
        BusinessException b => b.Message switch
        {
            IdpMessageKeys.ClientNotFound or IdpMessageKeys.ClientInactive
                or IdpMessageKeys.InvalidServiceAccount or IdpMessageKeys.ServiceAccountDisabled => InvalidClient,

            IdpMessageKeys.InvalidGrant or IdpMessageKeys.InvalidRedirectUri or IdpMessageKeys.PkceValidationFailed
                or IdpMessageKeys.RefreshTokenExpired or IdpMessageKeys.RefreshTokenReuseDetected
                or IdpMessageKeys.SessionInvalid or IdpMessageKeys.PrincipalNotFound
                or IdpMessageKeys.InvalidSubjectToken => InvalidGrant,

            IdpMessageKeys.UpscopingNotAllowed => InvalidScope,
            IdpMessageKeys.AudienceNotAllowed or IdpMessageKeys.InvalidResource => InvalidTarget,
            IdpMessageKeys.UnsupportedGrantType => UnsupportedGrantType,
            _ => InvalidRequest
        },
        _ => InvalidRequest
    };
}
