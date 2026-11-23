namespace Booster.Auth.Common.Internal.Constants.Messages;

public class IdpMessageKeys
{
    public const string UnsupportedResponseType = "idp.unsupported_response_type";
    public const string UnsupportedCodeChallengeType = "idp.unsupported_code_challenge_type";
    public const string ClientNotFound = "idp.client_not_found";
    public const string ClientInactive = "idp.client_inactive";
    public const string InvalidRedirectUri = "idp.invalid_redirect_uri";
    public const string CodeFlowNotSupported = "idp.code_flow_not_supported";
    public const string InvalidInteraction = "idp.invalid_interaction";
    public const string EmailRequired = "idp.email_required";
    public const string UserNameRequired = "idp.username_required";
    public const string PhoneRequired = "idp.phone_required";
    public const string IdentifierRequired = "idp.identifier_required";
    public const string PasswordTooShort = "idp.password_too_short";
    public const string PasswordRequiresUppercase = "idp.password_requires_uppercase";
    public const string PasswordRequiresLowercase = "idp.password_requires_lowercase";
    public const string PasswordRequiresDigit = "idp.password_requires_digit";
    public const string PasswordRequiresNonAlphanumeric = "idp.password_requires_non_alphanumeric";
    public const string IdentifierAlreadyExists = "idp.identifier_already_exists";
    public const string UserNameInvalid = "idp.username_invalid";
    public const string PasswordContainsWhitespace = "idp.password_contains_whitespace";
    public const string RegistrationNotAllowed = "idp.registration_not_allowed";
    public const string PasswordsDoNotMatch = "idp.passwords_do_not_match";
    public const string LocalLoginNotAllowed = "idp.local_login_not_allowed";
    public const string InvalidCredentials = "idp.invalid_credentials";
    public const string AccountLocked = "idp.account_locked";
    public const string ProviderNotAvailable = "idp.provider_not_available";
    public const string ProviderNotAllowed = "idp.provider_not_allowed";
    public const string UnsupportedGrantType = "idp.unsupported_grant_type";
    public const string InvalidResource = "idp.invalid_resource";
    public const string PkceValidationFailed = "idp.pkce_validation_failed";
    public const string InvalidGrant = "idp.invalid_grant";
    public const string PrincipalNotFound = "idp.principal_not_found";
    public const string UpscopingNotAllowed = "idp.upscoping_not_allowed";
    public const string RefreshTokenReuseDetected = "idp.refresh_token_reuse_detected";
    public const string RefreshTokenExpired = "idp.refresh_token_expired";
    public const string SessionInvalid = "idp.session_invalid";
    public const string InvalidClient = "idp.invalid_client";
    public const string ServiceAccountKeyRequired = "idp.service_account_key_required";
    public const string InvalidServiceAccount = "idp.invalid_service_account";
    public const string ServiceAccountDisabled = "idp.service_account_disabled";
    public const string ResourceRequiredForExchange = "idp.resource_required_for_exchange";
    public const string InvalidSubjectToken = "idp.invalid_subject_token";
    public const string AudienceNotAllowed = "idp.audience_not_allowed";
    public const string InvalidAccessToken = "idp.invalid_access_token";
    public const string LoggedOut = "idp.logged_out";

    public const string PasswordReused = "idp.password_reused";
    public const string PasswordExpired = "idp.password_expired";
    public const string CurrentPasswordInvalid = "idp.current_password_invalid";
    public const string ResetTokenInvalid = "idp.reset_token_invalid";
    public const string PasswordResetRequested = "idp.password_reset_requested";

    public const string LoginCodeInvalid = "idp.login_code_invalid";
    public const string LoginCodeExpired = "idp.login_code_expired";

    // --- Giriş doğrulaması (e-posta/telefon doğrulama, MFA) ------------------
    public const string LoginChallengeExpired = "idp.login_challenge_expired";
    public const string MfaMethodUnavailable = "idp.mfa_method_unavailable";


    // --- Federasyon ---------------------------------------------------------
    public const string FederationProviderNotFound = "idp.federation_provider_not_found";
    public const string FederationStateInvalid = "idp.federation_state_invalid";
    public const string FederationFailed = "idp.federation_failed";
    public const string FederationCancelled = "idp.federation_cancelled";
    public const string FederationEmailMissing = "idp.federation_email_missing";
    public const string FederationEmailNotVerified = "idp.federation_email_not_verified";
    public const string FederationJitDisabled = "idp.federation_jit_disabled";
    public const string LdapUnavailable = "idp.ldap_unavailable";
    public const string LdapJitDisabled = "idp.ldap_jit_disabled";
    public const string LdapLocalAccountExists = "idp.ldap_local_account_exists";
    public const string FederationAccountInactive = "idp.federation_account_inactive";
    public const string FederationLocalAccountExists = "idp.federation_local_account_exists";

    // --- Havuz kullanıcıları ve servis hesapları ---------------------------
    public const string PoolNotFound = "idp.pool_not_found";
    public const string RoleNotInPool = "idp.role_not_in_pool";
    public const string ScopeNotInPool = "idp.scope_not_in_pool";
    public const string ServiceAccountNotFound = "idp.service_account_not_found";
    public const string ConsentNotFound = "idp.consent_not_found";
    public const string SessionNotFound = "idp.session_not_found";
    public const string InvalidServiceAccountRelation = "idp.invalid_service_account_relation";
    public const string RoleNotHeldByCaller = "idp.role_not_held_by_caller";
    public const string ScopeNotHeldByCaller = "idp.scope_not_held_by_caller";

    // --- Uygulama logosu ----------------------------------------------------
    public const string ClientLogoRequired = "idp.client_logo_required";
    public const string ClientLogoTooLarge = "idp.client_logo_too_large";
    public const string ClientLogoInvalidFormat = "idp.client_logo_invalid_format";

    // --- Panelden kullanici ekleme / ice aktarma / davet ---------------------
    public const string InvitationInvalid = "idp.invitation_invalid";
    public const string InvitationNotPending = "idp.invitation_not_pending";
    public const string ImportCsvInvalid = "idp.import_csv_invalid";
    public const string ImportTooManyRows = "idp.import_too_many_rows";
    public const string DisplayNameRequired = "idp.display_name_required";
    public const string EmailInvalid = "idp.email_invalid";

    // --- Hesabim (son kullanicinin kendi hesabi) ------------------------------
    public const string AccountDisplayNameInvalid = "idp.account_display_name_invalid";
    public const string AccountPhoneInvalid = "idp.account_phone_invalid";
    public const string AccountContactUnchanged = "idp.account_contact_unchanged";
    public const string AccountCodeInvalid = "idp.account_code_invalid";
    public const string AccountDirectoryManaged = "idp.account_directory_managed";
    public const string AccountNoPassword = "idp.account_no_password";
    public const string AccountDeletionDisabled = "idp.account_deletion_disabled";
    public const string AccountDeletionConfirmRequired = "idp.account_deletion_confirm_required";
    public const string AccountUnavailable = "idp.account_unavailable";
}
