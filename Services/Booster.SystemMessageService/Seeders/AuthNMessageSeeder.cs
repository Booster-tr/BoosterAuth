using Booster.SystemMessageService.Seeders._base;

namespace Booster.SystemMessageService.Seeders;

/// <summary>Management API'nin (<c>authn.*</c>) mesajları.</summary>
public class AuthNMessageSeeder(IServiceScopeFactory scopeFactory, ILogger<AuthNMessageSeeder> logger)
    : SystemMessageSeederBase(scopeFactory, logger)
{
    protected override IReadOnlyList<Msg> Messages =>
    [
        // --- Bulunamadı ---
        new(AuthNMessageKeys.TenantNotFound, "Firma", "Firma bulunamadı.", "Tenant", "Tenant not found."),
        new(AuthNMessageKeys.InvalidRelation, "Yetkilendirme", "Geçersiz yetki. Verilebilecek yetkiler: owner, manager, viewer, grant_manager.", "Authorization", "Invalid permission. Assignable permissions are: owner, manager, viewer, grant_manager."),
        new(AuthNMessageKeys.UserPoolNotFound, "Havuz", "Kullanıcı havuzu bulunamadı.", "Pool", "User pool not found."),
        new(AuthNMessageKeys.RoleNotFound, "Rol", "Rol bulunamadı.", "Role", "Role not found."),
        new(AuthNMessageKeys.RoleScopeNotFound, "Rol yetkisi", "Role bağlı yetki bulunamadı.", "Role scope", "Role scope not found."),
        new(AuthNMessageKeys.ScopeDefinitionNotFound, "Yetki", "Yetki tanımı bulunamadı.", "Scope", "Scope definition not found."),
        new(AuthNMessageKeys.ClientNotFound, "Uygulama", "Uygulama bulunamadı.", "Client", "Client not found."),

        // --- Firma yetkileri ---
        new(AuthNMessageKeys.TenantOwnerRelationRequiresOwner, "Yetkilendirme", "Sahip yetkisini yalnızca firma sahipleri verebilir veya geri alabilir.", "Authorization", "Only tenant owners can grant or revoke the owner permission."),
        new(AuthNMessageKeys.TenantOwnerAccessCannotBeRevoked, "Yetkilendirme", "Firma sahibinin yetkileri geri alınamaz.", "Authorization", "The tenant owner's permissions cannot be revoked."),
        new(AuthNMessageKeys.PrincipalIdentifierInvalid, "Kullanıcı", "Geçerli bir e-posta adresi, kullanıcı adı veya telefon numarası girin.", "User", "Enter a valid email address, username or phone number."),
        new(AuthNMessageKeys.PrincipalNotFound, "Kullanıcı", "Bu bilgilere sahip bir kullanıcı bulunamadı.", "User", "No user was found with this information."),
        new(AuthNMessageKeys.PrincipalLookupLimitExceeded, "Kullanıcı", "Çok fazla deneme yaptınız. Lütfen bir süre sonra tekrar deneyin.", "User", "Too many attempts. Please try again later."),

        // --- Benzersizlik ---
        new(AuthNMessageKeys.RoleKeyAlreadyExists, "Rol", "Bu rol anahtarı zaten kullanımda.", "Role", "This role key is already in use."),
        new(AuthNMessageKeys.ScopeAlreadyDefined, "Yetki", "Bu yetki zaten tanımlı.", "Scope", "This scope is already defined."),
        new(AuthNMessageKeys.RoleScopeAlreadyAdded, "Rol yetkisi", "Bu yetki role zaten eklenmiş.", "Role scope", "This scope is already added to the role."),
        new(AuthNMessageKeys.ClientKeyAlreadyExists, "Uygulama", "Bu uygulama anahtarı zaten kullanımda.", "Client", "This client key is already in use."),

        // --- Biçim ---
        new(AuthNMessageKeys.RoleKeyInvalidFormat, "Rol", "Geçersiz rol anahtarı biçimi.", "Role", "Invalid role key format."),
        new(AuthNMessageKeys.ScopeInvalidFormat, "Yetki", "Geçersiz yetki biçimi.", "Scope", "Invalid scope format."),
        new(AuthNMessageKeys.ClientKeyInvalidFormat, "Uygulama", "Geçersiz uygulama anahtarı biçimi.", "Client", "Invalid client key format."),

        // --- Durum / ilişki ---
        new(AuthNMessageKeys.ScopeNotDefinedInPool, "Yetki", "Bu yetki havuzda tanımlı değil.", "Scope", "This scope is not defined in the pool."),
        new(AuthNMessageKeys.ActiveChildrenExist, "Durum", "Altında aktif kayıtlar var; önce onları pasife çekin.", "Status", "There are still active records underneath; deactivate them first."),
        new(AuthNMessageKeys.ParentNotActive, "Durum", "Bağlı olduğu üst kayıt aktif değil; önce onu aktifleştirin.", "Status", "The parent record is not active; activate it first."),

        // --- Uygulama ayarları ---
        new(AuthNMessageKeys.ClientGrantTypeRequired, "Uygulama", "En az bir grant tipi seçmelisiniz.", "Client", "At least one grant type is required."),
        new(AuthNMessageKeys.ClientRedirectUriRequired, "Uygulama", "Yönlendirme adresi zorunludur.", "Client", "A redirect URI is required."),
        new(AuthNMessageKeys.ClientRedirectUriInvalid, "Uygulama", "Geçersiz yönlendirme adresi.", "Client", "Invalid redirect URI."),

        // --- Token scope politikası ---
        new(AuthNMessageKeys.TokenScopePolicyMax, "Token politikası", "İzin verilen politika sayısı aşıldı.", "Token policy", "The maximum number of policies has been exceeded."),
        new(AuthNMessageKeys.TokenScopePolicyKeyRequired, "Token politikası", "Politika anahtarı zorunludur.", "Token policy", "The policy key is required."),
        new(AuthNMessageKeys.TokenScopePolicyRequiredPermissionsRequired, "Token politikası", "Zorunlu politika için en az bir izin belirtmelisiniz.", "Token policy", "A required policy must list at least one permission."),
        new(AuthNMessageKeys.TokenScopePolicyPermissionEmpty, "Token politikası", "İzin adı boş olamaz.", "Token policy", "A permission name cannot be empty."),
        new(AuthNMessageKeys.TokenScopePolicyPermissionDuplicate, "Token politikası", "Aynı izin birden fazla kez verilmiş.", "Token policy", "The same permission is listed more than once."),
        new(AuthNMessageKeys.TokenScopePolicyPermissionsNotAllowed, "Token politikası", "Bu izinler uygulama için tanımlı değil.", "Token policy", "These permissions are not defined for the client."),

        // --- Dış kimlik sağlayıcıları ---
        new(AuthNMessageKeys.IdentityProviderNotFound, "Sağlayıcı", "Sağlayıcı bulunamadı.", "Provider", "The provider was not found."),
        new(AuthNMessageKeys.IdentityProviderKeyAlreadyExists, "Sağlayıcı", "Bu sağlayıcı anahtarı havuzda zaten kullanımda.", "Provider", "This provider key is already in use in the pool."),
        new(AuthNMessageKeys.IdentityProviderKeyInvalidFormat, "Sağlayıcı", "Sağlayıcı anahtarı yalnızca küçük harf, rakam ve tire içerebilir.", "Provider", "The provider key may contain only lowercase letters, digits and hyphens."),
        new(AuthNMessageKeys.IdentityProviderTypeUnknown, "Sağlayıcı", "Bu sağlayıcı türü desteklenmiyor.", "Provider", "This provider type is not supported."),
        new(AuthNMessageKeys.IdentityProviderClientIdRequired, "Sağlayıcı", "İstemci kimliği (client_id) zorunludur.", "Provider", "The client id is required."),
        new(AuthNMessageKeys.IdentityProviderIssuerRequired, "Sağlayıcı", "Issuer adresi (issuer_url) zorunludur.", "Provider", "The issuer url is required."),
        new(AuthNMessageKeys.IdentityProviderSamlSettingsRequired, "Sağlayıcı", "SAML için sp_entity_id, sign_on_url ve signing_certificate zorunludur.", "Provider", "SAML requires sp_entity_id, sign_on_url and signing_certificate."),
    ];
}
