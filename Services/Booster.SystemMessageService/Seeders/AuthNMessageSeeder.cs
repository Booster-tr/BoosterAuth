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
        new(AuthNMessageKeys.ManagementPoolClientsReserved, "Uygulama", "Yönetim havuzunun uygulamaları sistem tarafından yönetilir; bu havuzda uygulama eklenemez ya da değiştirilemez. Uygulamanızı kendi havuzunuzda oluşturun.", "Client", "Applications of the management pool are managed by the system and cannot be added or changed. Create your application in your own pool."),

        // --- Firma yetkileri ---
        new(AuthNMessageKeys.ClientOwnerRelationRequiresOwner, "Yetkilendirme", "Sahip yetkisini yalnızca uygulama sahipleri verebilir veya geri alabilir.", "Authorization", "Only application owners can grant or revoke the owner permission."),
        new(AuthNMessageKeys.PoolOwnerRelationRequiresOwner, "Yetkilendirme", "Sahip yetkisini yalnızca havuz sahipleri verebilir veya geri alabilir.", "Authorization", "Only pool owners can grant or revoke the owner permission."),
        new(AuthNMessageKeys.TenantOwnerRelationRequiresOwner, "Yetkilendirme", "Sahip yetkisini yalnızca firma sahipleri verebilir veya geri alabilir.", "Authorization", "Only tenant owners can grant or revoke the owner permission."),
        new(AuthNMessageKeys.TenantOwnerAccessCannotBeRevoked, "Yetkilendirme", "Firma sahibinin yetkileri geri alınamaz.", "Authorization", "The tenant owner's permissions cannot be revoked."),
        new(AuthNMessageKeys.PrincipalIdentifierInvalid, "Kullanıcı", "Geçerli bir e-posta adresi, kullanıcı adı veya telefon numarası girin.", "User", "Enter a valid email address, username or phone number."),
        new(AuthNMessageKeys.PrincipalNotFound, "Kullanıcı", "Bu bilgilere sahip bir kullanıcı bulunamadı.", "User", "No user was found with this information."),
        new(AuthNMessageKeys.PrincipalLookupLimitExceeded, "Kullanıcı", "Çok fazla deneme yaptınız. Lütfen bir süre sonra tekrar deneyin.", "User", "Too many attempts. Please try again later."),

        // --- Havuz politikası ---
        new(AuthNMessageKeys.ValueOutOfRange, "Politika", "Değer {From} ile {To} arasında olmalıdır.", "Policy", "The value must be between {From} and {To}."),
        new(AuthNMessageKeys.LoginProviderRequired, "Politika", "En az bir giriş yöntemi seçmelisiniz.", "Policy", "Select at least one sign-in method."),
        new(AuthNMessageKeys.LoginIdentifierRequired, "Politika", "En az bir kullanıcı tanımlayıcısı seçmelisiniz.", "Policy", "Select at least one user identifier."),
        new(AuthNMessageKeys.CountryCodeInvalid, "Politika", "Ülke kodu 1-4 haneli bir sayı olmalıdır.", "Policy", "The country code must be a 1-4 digit number."),
        new(AuthNMessageKeys.MfaMethodRequired, "Politika", "Çok faktörlü doğrulama zorunluysa en az bir yöntem seçmelisiniz.", "Policy", "Select at least one method when multi-factor authentication is required."),
        new(AuthNMessageKeys.RefreshTtlShorterThanAccess, "Politika", "Yenileme token süresi erişim token süresinden kısa olamaz.", "Policy", "The refresh token lifetime cannot be shorter than the access token lifetime."),

        // --- Benzersizlik ---
        new(AuthNMessageKeys.RoleKeyAlreadyExists, "Rol", "Bu rol anahtarı zaten kullanımda.", "Role", "This role key is already in use."),
        new(AuthNMessageKeys.ScopeAlreadyDefined, "Yetki", "Bu yetki zaten tanımlı.", "Scope", "This scope is already defined."),
        new(AuthNMessageKeys.RoleScopeAlreadyAdded, "Rol yetkisi", "Bu yetki role zaten eklenmiş.", "Role scope", "This scope is already added to the role."),
        new(AuthNMessageKeys.ClientKeyAlreadyExists, "Uygulama", "Bu uygulama anahtarı zaten kullanımda.", "Client", "This client key is already in use."),
        new(AuthNMessageKeys.RedirectDomainNotAllowed, "Yönlendirme adresi", "Bu adresler havuzun izin verilen domain politikasına uymuyor: {Uris}", "Redirect URI", "These addresses do not comply with the pool's allowed domain policy: {Uris}"),
        new(AuthNMessageKeys.RedirectDomainInvalid, "Yönlendirme politikası", "Geçerli bir domain girin (ör. firma.com). Test adresleri için ayrı seçeneği kullanın.", "Redirect policy", "Enter a valid domain (e.g. company.com). Use the separate option for test addresses."),
        new(AuthNMessageKeys.RedirectDomainsRequired, "Yönlendirme politikası", "Politika açıkken en az bir domain girin ya da test adreslerine / mobil şemalara izin verin.", "Redirect policy", "While the policy is on, enter at least one domain or allow test addresses / mobile schemes."),

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
        new(AuthNMessageKeys.ClientAudienceInvalid, "Uygulama", "İzin verilen API adresi tam bir http(s) adresi olmalı (ör. https://api.firma.com); # ve sorgu parametresi içeremez.", "Client", "An allowed API must be a full http(s) URL (e.g. https://api.company.com) without # or query parameters."),

        // --- Token scope politikası ---

        // --- Dış kimlik sağlayıcıları ---
        new(AuthNMessageKeys.IdentityProviderNotFound, "Sağlayıcı", "Sağlayıcı bulunamadı.", "Provider", "The provider was not found."),
        new(AuthNMessageKeys.IdentityProviderLdapSettingsRequired, "LDAP", "LDAP için sunucu, bind DN ve arama kökü (base DN) zorunludur.", "LDAP", "LDAP requires a server, a bind DN and a search base (base DN)."),
        new(AuthNMessageKeys.IdentityProviderLdapSettingsInvalid, "LDAP", "LDAP ayarları geçersiz: port 1-65535 olmalı, güvenlik ldaps / starttls / none olmalı, kullanıcı filtresi parantez içinde ve {0} içermeli.", "LDAP", "Invalid LDAP settings: port must be 1-65535, security ldaps / starttls / none, and the user filter must be parenthesized and contain {0}."),
        new(AuthNMessageKeys.IdentityProviderLdapPasswordRequired, "LDAP", "Servis hesabı (bind) parolası zorunludur.", "LDAP", "The service account (bind) password is required."),
        new(AuthNMessageKeys.IdentityProviderLdapAlreadyExists, "LDAP", "Bu havuzda zaten bir LDAP bağlantısı var; mevcut olanı düzenleyin.", "LDAP", "This pool already has an LDAP connection; edit the existing one."),
        new(AuthNMessageKeys.IdentityProviderKeyAlreadyExists, "Sağlayıcı", "Bu sağlayıcı anahtarı havuzda zaten kullanımda.", "Provider", "This provider key is already in use in the pool."),
        new(AuthNMessageKeys.IdentityProviderKeyInvalidFormat, "Sağlayıcı", "Sağlayıcı anahtarı yalnızca küçük harf, rakam ve tire içerebilir.", "Provider", "The provider key may contain only lowercase letters, digits and hyphens."),
        new(AuthNMessageKeys.IdentityProviderTypeUnknown, "Sağlayıcı", "Bu sağlayıcı türü desteklenmiyor.", "Provider", "This provider type is not supported."),
        new(AuthNMessageKeys.IdentityProviderClientIdRequired, "Sağlayıcı", "İstemci kimliği (client_id) zorunludur.", "Provider", "The client id is required."),
        new(AuthNMessageKeys.IdentityProviderIssuerRequired, "Sağlayıcı", "Issuer adresi (issuer_url) zorunludur.", "Provider", "The issuer url is required."),
        new(AuthNMessageKeys.IdentityProviderSamlSettingsRequired, "Sağlayıcı", "SAML için sp_entity_id, sign_on_url ve signing_certificate zorunludur.", "Provider", "SAML requires sp_entity_id, sign_on_url and signing_certificate."),
    ];
}
