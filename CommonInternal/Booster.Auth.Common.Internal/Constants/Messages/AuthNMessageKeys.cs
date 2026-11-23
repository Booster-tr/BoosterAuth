namespace Booster.Auth.Common.Internal.Constants.Messages;

public class AuthNMessageKeys
{
    // --- Bulunamadı ---
    public const string TenantNotFound = "authn.tenant_not_found";
    public const string InvalidRelation = "authn.invalid_relation";
    public const string UserPoolNotFound = "authn.user_pool_not_found";
    public const string RoleNotFound = "authn.role_not_found";
    public const string RoleScopeNotFound = "authn.role_scope_not_found";
    public const string ScopeDefinitionNotFound = "authn.scope_definition_not_found";
    public const string ClientNotFound = "authn.client_not_found";

    // --- Firma yetkileri ---
    public const string TenantOwnerAccessCannotBeRevoked = "authn.tenant_owner_access_cannot_be_revoked";
    public const string TenantOwnerRelationRequiresOwner = "authn.tenant_owner_relation_requires_owner";
    public const string PoolOwnerRelationRequiresOwner = "authn.pool_owner_relation_requires_owner";
    public const string ClientOwnerRelationRequiresOwner = "authn.client_owner_relation_requires_owner";
    public const string PrincipalIdentifierInvalid = "authn.principal_identifier_invalid";
    public const string PrincipalNotFound = "authn.principal_not_found";
    public const string PrincipalLookupLimitExceeded = "authn.principal_lookup_limit_exceeded";

    // --- Havuz politikası ---
    public const string ValueOutOfRange = "authn.value_out_of_range";
    public const string LoginProviderRequired = "authn.login_provider_required";
    public const string LoginIdentifierRequired = "authn.login_identifier_required";
    public const string CountryCodeInvalid = "authn.country_code_invalid";
    public const string MfaMethodRequired = "authn.mfa_method_required";
    public const string RefreshTtlShorterThanAccess = "authn.refresh_ttl_shorter_than_access";

    // --- Benzersizlik ---
    public const string RoleKeyAlreadyExists = "authn.role_key_already_exists";
    public const string ScopeAlreadyDefined = "authn.scope_already_defined";
    public const string RoleScopeAlreadyAdded = "authn.role_scope_already_added";
    public const string ClientKeyAlreadyExists = "authn.client_key_already_exists";

    // --- Redirect domain politikasi (havuz guvenlik ayari; lisansla ilgisi yok) ---
    public const string RedirectDomainNotAllowed = "authn.redirect_domain_not_allowed";
    public const string RedirectDomainInvalid = "authn.redirect_domain_invalid";
    public const string RedirectDomainsRequired = "authn.redirect_domains_required";

    // --- Biçim ---
    public const string RoleKeyInvalidFormat = "authn.role_key_invalid_format";
    public const string ScopeInvalidFormat = "authn.scope_invalid_format";
    public const string ClientKeyInvalidFormat = "authn.client_key_invalid_format";

    // --- Durum / ilişki ---
    public const string ScopeNotDefinedInPool = "authn.scope_not_defined_in_pool";
    public const string ActiveChildrenExist = "authn.active_children_exist";
    public const string ParentNotActive = "authn.parent_not_active";

    // --- Client ---
    public const string ClientGrantTypeRequired = "authn.client_grant_type_required";
    public const string ClientRedirectUriRequired = "authn.client_redirect_uri_required";
    public const string ClientRedirectUriInvalid = "authn.client_redirect_uri_invalid";

    // --- Token scope policy ---
    public const string TokenScopePolicyMax = "authn.token_scope_policy_max";
    public const string TokenScopePolicyKeyRequired = "authn.token_scope_policy_key_required";
    public const string TokenScopePolicyKeyDuplicate = "authn.token_scope_policy_key_duplicate";
    public const string TokenScopePolicyKeyInvalid = "authn.token_scope_policy_key_invalid";
    public const string TokenScopePolicyRequiredPermissionsRequired = "authn.token_scope_policy_required_permissions_required";
    public const string TokenScopePolicyPermissionEmpty = "authn.token_scope_policy_permission_empty";
    public const string TokenScopePolicyPermissionDuplicate = "authn.token_scope_policy_permission_duplicate";
    public const string TokenScopePolicyPermissionsNotAllowed = "authn.token_scope_policy_permissions_not_allowed";

    // --- Dis kimlik saglayicilari -----------------------------------------
    public const string IdentityProviderNotFound = "authn.identity_provider_not_found";
    public const string IdentityProviderKeyAlreadyExists = "authn.identity_provider_key_already_exists";
    public const string IdentityProviderKeyInvalidFormat = "authn.identity_provider_key_invalid_format";
    public const string IdentityProviderTypeUnknown = "authn.identity_provider_type_unknown";
    public const string IdentityProviderClientIdRequired = "authn.identity_provider_client_id_required";
    public const string IdentityProviderIssuerRequired = "authn.identity_provider_issuer_required";
    public const string IdentityProviderSamlSettingsRequired = "authn.identity_provider_saml_settings_required";
}
