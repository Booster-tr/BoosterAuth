using Booster.SystemMessageService.Seeders._base;

namespace Booster.SystemMessageService.Seeders;

/// <summary>Kimlik sağlayıcının (<c>idp.*</c>) mesajları — giriş, kayıt ve token akışları.</summary>
public class IdpMessageSeeder(IServiceScopeFactory scopeFactory, ILogger<IdpMessageSeeder> logger)
    : SystemMessageSeederBase(scopeFactory, logger)
{
    protected override IReadOnlyList<Msg> Messages =>
    [
        // --- Yetkilendirme isteği (/authorize) ---
        new(IdpMessageKeys.UnsupportedResponseType, "İstek", "Bu yanıt tipi desteklenmiyor.", "Request", "This response type is not supported."),
        new(IdpMessageKeys.UnsupportedCodeChallengeType, "İstek", "Bu PKCE yöntemi desteklenmiyor.", "Request", "This PKCE method is not supported."),
        new(IdpMessageKeys.ClientNotFound, "Uygulama", "Giriş yapmak istediğiniz uygulama tanımlı değil.", "Client", "The application you are signing in to is not registered."),
        new(IdpMessageKeys.ClientInactive, "Uygulama", "Bu uygulamanın erişimi geçici olarak durduruldu.", "Client", "This application's access is temporarily suspended."),
        new(IdpMessageKeys.InvalidRedirectUri, "Yönlendirme", "Güvenliğiniz için bu adrese yönlendirme yapılamıyor.", "Redirect", "For your safety, we cannot redirect to this address."),
        new(IdpMessageKeys.CodeFlowNotSupported, "Uygulama", "Bu uygulama yetkilendirme kodu akışını kullanamaz.", "Client", "This application cannot use the authorization code flow."),
        new(IdpMessageKeys.InvalidInteraction, "Giriş bağlantısı", "Bu giriş bağlantısının süresi dolmuş ya da artık geçerli değil.", "Sign-in link", "This sign-in link has expired or is no longer valid."),
        new(IdpMessageKeys.InvalidResource, "İstek", "İstenen kaynak bu uygulama için tanımlı değil.", "Request", "The requested resource is not defined for this application."),

        // --- Kayıt formu ---
        new(IdpMessageKeys.EmailRequired, "E-posta", "E-posta adresi zorunludur.", "Email", "An email address is required."),
        new(IdpMessageKeys.UserNameRequired, "Kullanıcı adı", "Kullanıcı adı zorunludur.", "Username", "A username is required."),
        new(IdpMessageKeys.PhoneRequired, "Telefon", "Telefon numarası zorunludur.", "Phone", "A phone number is required."),
        new(IdpMessageKeys.IdentifierRequired, "Kimlik bilgisi", "En az bir kimlik bilgisi girmelisiniz.", "Identifier", "At least one identifier is required."),
        new(IdpMessageKeys.IdentifierAlreadyExists, "Kimlik bilgisi", "Bu kimlik bilgisi zaten kayıtlı.", "Identifier", "This identifier is already registered."),
        new(IdpMessageKeys.UserNameInvalid, "Kullanıcı adı", "Kullanıcı adı yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.", "Username", "A username may contain only letters, digits, dot, underscore and hyphen."),
        new(IdpMessageKeys.RegistrationNotAllowed, "Kayıt", "Bu uygulama üzerinden yeni hesap açılmasına izin verilmiyor.", "Sign-up", "Creating a new account through this application is not allowed."),

        // --- Parola kuralları ---
        new(IdpMessageKeys.PasswordTooShort, "Parola", "Parola çok kısa.", "Password", "The password is too short."),
        new(IdpMessageKeys.PasswordRequiresUppercase, "Parola", "Parola en az bir büyük harf içermeli.", "Password", "The password must contain an uppercase letter."),
        new(IdpMessageKeys.PasswordRequiresLowercase, "Parola", "Parola en az bir küçük harf içermeli.", "Password", "The password must contain a lowercase letter."),
        new(IdpMessageKeys.PasswordRequiresDigit, "Parola", "Parola en az bir rakam içermeli.", "Password", "The password must contain a digit."),
        new(IdpMessageKeys.PasswordRequiresNonAlphanumeric, "Parola", "Parola en az bir sembol içermeli.", "Password", "The password must contain a symbol."),
        new(IdpMessageKeys.PasswordContainsWhitespace, "Parola", "Parola boşluk içeremez.", "Password", "The password cannot contain spaces."),
        new(IdpMessageKeys.PasswordsDoNotMatch, "Parola", "Parolalar eşleşmiyor.", "Password", "The passwords do not match."),

        // --- Giriş ---
        new(IdpMessageKeys.LocalLoginNotAllowed, "Giriş", "Bu uygulamada parolayla giriş kapalı.", "Sign-in", "Password sign-in is disabled for this application."),
        new(IdpMessageKeys.InvalidCredentials, "Giriş", "Kullanıcı adı veya parola hatalı.", "Sign-in", "The username or password is incorrect."),
        new(IdpMessageKeys.AccountLocked, "Hesap", "Çok fazla hatalı deneme nedeniyle hesabınız geçici olarak kilitlendi.", "Account", "Your account is temporarily locked after too many failed attempts."),
        new(IdpMessageKeys.ProviderNotAvailable, "Giriş yöntemi", "Seçtiğiniz giriş yöntemi şu anda kullanılamıyor.", "Sign-in method", "The selected sign-in method is currently unavailable."),
        new(IdpMessageKeys.ProviderNotAllowed, "Giriş yöntemi", "Bu uygulamada seçtiğiniz giriş yöntemine izin verilmiyor.", "Sign-in method", "The selected sign-in method is not allowed for this application."),
        new(IdpMessageKeys.LoggedOut, "Çıkış", "Güvenle çıkış yaptınız.", "Sign-out", "You have been signed out."),

        // --- Token ucu ---
        new(IdpMessageKeys.UnsupportedGrantType, "Token", "Bu grant tipi desteklenmiyor.", "Token", "This grant type is not supported."),
        new(IdpMessageKeys.PkceValidationFailed, "Token", "PKCE doğrulaması başarısız.", "Token", "PKCE verification failed."),
        new(IdpMessageKeys.InvalidGrant, "Token", "Yetkilendirme bilgisi geçersiz ya da süresi dolmuş.", "Token", "The grant is invalid or has expired."),
        new(IdpMessageKeys.InvalidClient, "Uygulama", "Uygulamanın kimliği doğrulanamadı.", "Client", "The application could not be authenticated."),
        new(IdpMessageKeys.PrincipalNotFound, "Kullanıcı", "Kullanıcı bulunamadı.", "User", "The user was not found."),
        new(IdpMessageKeys.PoolNotFound, "Havuz", "Kullanıcı havuzu bulunamadı.", "Pool", "The user pool was not found."),
        new(IdpMessageKeys.RoleNotInPool, "Rol", "Seçilen rollerden biri bu havuzda tanımlı değil.", "Role", "One of the selected roles is not defined in this pool."),
        new(IdpMessageKeys.ScopeNotInPool, "Kapsam", "Seçilen kapsamlardan biri bu havuzda tanımlı değil.", "Scope", "One of the selected scopes is not defined in this pool."),
        new(IdpMessageKeys.ConsentNotFound, "Onay", "Onay kaydı bulunamadı.", "Consent", "The consent was not found."),
        new(IdpMessageKeys.SessionNotFound, "Oturum", "Oturum bulunamadı.", "Session", "The session was not found."),
        new(IdpMessageKeys.ServiceAccountNotFound, "Servis hesabı", "Servis hesabı bulunamadı.", "Service account", "The service account was not found."),
        new(IdpMessageKeys.InvalidServiceAccountRelation, "Servis hesabı", "Servis hesabına verilebilecek yetkiler: viewer, manager, grant_manager.", "Service account", "A service account can be given: viewer, manager, grant_manager."),
        new(IdpMessageKeys.RoleNotHeldByCaller, "Rol", "Yalnızca kendinizde olan rolleri servis hesabına verebilirsiniz.", "Role", "You can only give a service account roles you have yourself."),
        new(IdpMessageKeys.ScopeNotHeldByCaller, "Kapsam", "Yalnızca kendinizde olan kapsamları servis hesabına verebilirsiniz.", "Scope", "You can only give a service account scopes you have yourself."),
        new(IdpMessageKeys.ClientLogoRequired, "Logo", "Lütfen bir logo dosyası seçin.", "Logo", "Please choose a logo file."),
        new(IdpMessageKeys.ClientLogoTooLarge, "Logo", "Logo dosyası en fazla 1 MB olabilir.", "Logo", "The logo file can be at most 1 MB."),
        new(IdpMessageKeys.ClientLogoInvalidFormat, "Logo", "Logo PNG, JPEG, GIF ya da WebP biçiminde olmalıdır.", "Logo", "The logo must be a PNG, JPEG, GIF or WebP image."),
        new(IdpMessageKeys.UpscopingNotAllowed, "Yetki", "Var olandan daha geniş yetki istenemez.", "Scope", "You cannot request broader scopes than already granted."),
        new(IdpMessageKeys.RefreshTokenReuseDetected, "Oturum", "Yenileme jetonu tekrar kullanıldı; güvenlik için tüm oturum kapatıldı.", "Session", "The refresh token was reused; the whole session was revoked for safety."),
        new(IdpMessageKeys.RefreshTokenExpired, "Oturum", "Yenileme jetonunun süresi doldu. Lütfen yeniden giriş yapın.", "Session", "The refresh token has expired. Please sign in again."),
        new(IdpMessageKeys.SessionInvalid, "Oturum", "Oturumunuz geçerli değil. Lütfen yeniden giriş yapın.", "Session", "Your session is not valid. Please sign in again."),
        new(IdpMessageKeys.InvalidAccessToken, "Token", "Erişim jetonu geçersiz.", "Token", "The access token is invalid."),
        new(IdpMessageKeys.InvalidSubjectToken, "Token", "Takas edilecek jeton geçersiz.", "Token", "The subject token is invalid."),
        new(IdpMessageKeys.ResourceRequiredForExchange, "Token", "Jeton takası için hedef kaynak belirtilmelidir.", "Token", "A target resource is required for token exchange."),
        new(IdpMessageKeys.AudienceNotAllowed, "Token", "Bu hedef kitle uygulama için tanımlı değil.", "Token", "This audience is not allowed for the application."),

        // --- Parolasız giriş ---
        new(IdpMessageKeys.LoginCodeInvalid, "Giriş kodu", "Girdiğiniz kod hatalı. Lütfen kontrol edip tekrar deneyin.", "Sign-in code", "The code you entered is incorrect. Please check it and try again."),
        new(IdpMessageKeys.LoginCodeExpired, "Giriş kodu", "Kodun süresi doldu. Lütfen yeni bir kod isteyin.", "Sign-in code", "The code has expired. Please request a new one."),

        // --- Giriş doğrulaması (e-posta/telefon doğrulama, MFA) ---
        new(IdpMessageKeys.LoginChallengeExpired, "Doğrulama", "Doğrulama süresi doldu. Lütfen yeniden giriş yapın.", "Verification", "The verification has expired. Please sign in again."),
        new(IdpMessageKeys.MfaMethodUnavailable, "İki adımlı doğrulama", "Hesabınızda iki adımlı doğrulama kodu gönderilebilecek bir e-posta ya da telefon yok. Lütfen yöneticinize başvurun.", "Two-step verification", "Your account has no email address or phone number to send a two-step verification code to. Please contact your administrator."),
        new(IdpMessageKeys.MfaSecondChannelUnavailable, "İki adımlı doğrulama", "Bu uygulama iki adımlı doğrulama istiyor ve hesabınızda kodun gittiği kanal dışında ikinci bir kanal (e-posta ya da telefon) yok. Lütfen parolanızla giriş yapın.", "Two-step verification", "This application requires two-step verification and your account has no second channel (email or phone) besides the one the code was sent to. Please sign in with your password."),

        // --- Federasyon (dış sağlayıcıyla giriş) ---
        new(IdpMessageKeys.FederationProviderNotFound, "Sağlayıcı", "Bu giriş yöntemi tanımlı değil ya da kapatılmış.", "Provider", "This sign-in method is not configured or has been disabled."),
        new(IdpMessageKeys.FederationStateInvalid, "Giriş", "Giriş isteğinin süresi dolmuş. Lütfen baştan deneyin.", "Sign-in", "The sign-in request has expired. Please start again."),
        new(IdpMessageKeys.FederationFailed, "Giriş", "Sağlayıcıyla giriş tamamlanamadı. Lütfen tekrar deneyin.", "Sign-in", "Sign-in with the provider could not be completed. Please try again."),
        new(IdpMessageKeys.FederationCancelled, "Giriş", "Giriş isteğini onaylamadınız.", "Sign-in", "You did not approve the sign-in request."),
        new(IdpMessageKeys.FederationEmailMissing, "E-posta", "Sağlayıcı bize bir e-posta adresi vermedi; bu yöntemle hesap açılamıyor.", "Email", "The provider did not give us an email address, so an account cannot be created this way."),
        new(IdpMessageKeys.FederationEmailNotVerified, "E-posta", "Sağlayıcıdaki e-posta adresiniz doğrulanmamış. Önce orada doğrulayın.", "Email", "Your email address is not verified at the provider. Please verify it there first."),
        new(IdpMessageKeys.LdapUnavailable, "Giriş", "Kurum dizinine şu an ulaşılamıyor. Lütfen biraz sonra tekrar deneyin.", "Sign-in", "The corporate directory cannot be reached right now. Please try again shortly."),
        new(IdpMessageKeys.LdapJitDisabled, "Giriş", "Kurum dizini hesabınız doğrulandı ancak bu uygulamada hesabınız yok. Yöneticinize başvurun.", "Sign-in", "Your directory account was verified, but you do not have an account in this application. Contact your administrator."),
        new(IdpMessageKeys.LdapLocalAccountExists, "Giriş", "Bu bilgilerle parolalı bir hesap zaten var. Kendi parolanızla giriş yapın ya da yöneticinize başvurun.", "Sign-in", "An account with a password already exists for these details. Sign in with that password or contact your administrator."),
        new(IdpMessageKeys.FederationJitDisabled, "Kayıt", "Bu uygulamada dış sağlayıcıyla yeni hesap açılamıyor.", "Registration", "Creating a new account through an external provider is not allowed for this application."),
        new(IdpMessageKeys.FederationAccountInactive, "Hesap", "Hesabınızın erişimi durdurulmuş.", "Account", "Your account access has been suspended."),
        new(IdpMessageKeys.FederationLocalAccountExists, "Hesap", "Bu e-posta adresiyle sisteme daha önce giriş yapılmış. Parolanızla girebilir ya da parolanızı sıfırlayabilirsiniz.", "Account", "This email address has signed in here before. You can sign in with your password or reset it."),

        // --- Parola yönetimi ---
        new(IdpMessageKeys.PasswordReused, "Parola", "Bu parolayı daha önce kullanmıştınız. Lütfen farklı bir parola seçin.", "Password", "You have used this password before. Please choose a different one."),
        new(IdpMessageKeys.PasswordExpired, "Parola", "Parolanızın süresi doldu. Devam etmek için yeni bir parola belirleyin.", "Password", "Your password has expired. Set a new one to continue."),
        new(IdpMessageKeys.CurrentPasswordInvalid, "Parola", "Mevcut parolanız hatalı.", "Password", "Your current password is incorrect."),
        new(IdpMessageKeys.ResetTokenInvalid, "Sıfırlama bağlantısı", "Bu sıfırlama bağlantısı geçersiz ya da süresi dolmuş. Lütfen yeniden talep edin.", "Reset link", "This reset link is invalid or has expired. Please request a new one."),
        new(IdpMessageKeys.PasswordResetRequested, "Parola sıfırlama", "Girdiğiniz bilgiye ait bir hesap varsa sıfırlama bağlantısı gönderildi.", "Password reset", "If an account matches what you entered, a reset link has been sent."),

        // --- Hesabım (son kullanıcının kendi hesabı) ---
        new(IdpMessageKeys.AccountDisplayNameInvalid, "Ad soyad", "Ad soyad boş olamaz ve en fazla 300 karakter olabilir.", "Name", "Your name cannot be empty and can be at most 300 characters."),
        new(IdpMessageKeys.AccountPhoneInvalid, "Telefon", "Telefon numarasını ülke koduyla girin (ör. +90 532 000 00 00).", "Phone", "Enter the phone number with its country code (e.g. +44 7700 900000)."),
        new(IdpMessageKeys.AccountContactUnchanged, "Hesap", "Bu zaten hesabınızda kayıtlı olan bilgi.", "Account", "This is already the information on your account."),
        new(IdpMessageKeys.AccountCodeInvalid, "Doğrulama kodu", "Kod hatalı ya da süresi dolmuş. Gerekirse yeni bir kod isteyin.", "Verification code", "The code is incorrect or has expired. Request a new code if needed."),
        new(IdpMessageKeys.AccountDirectoryManaged, "Hesap", "Bu bilgi kurum dizininden yönetiliyor. Değiştirmek için yöneticinize başvurun.", "Account", "This information is managed by your organization's directory. Contact your administrator to change it."),
        new(IdpMessageKeys.AccountNoPassword, "Parola", "Hesabınızda parola tanımlı değil; giriş yönteminizle oturum açmaya devam edin.", "Password", "Your account has no password; keep signing in with your current method."),
        new(IdpMessageKeys.AccountDeletionDisabled, "Hesap", "Bu uygulamada hesap silme kapalı. Hesabınızın silinmesi için yöneticinize başvurun.", "Account", "Account deletion is turned off for this application. Contact your administrator to have your account deleted."),
        new(IdpMessageKeys.AccountDeletionConfirmRequired, "Onay", "Devam etmek için hesabınızın kalıcı olarak silineceğini onaylayın.", "Confirmation", "Confirm that your account will be permanently deleted to continue."),
        new(IdpMessageKeys.AccountUnavailable, "Hesap", "Hesap sayfası bu uygulama için kullanılamıyor.", "Account", "The account page is not available for this application."),
        new(IdpMessageKeys.LegalDocumentNotFound, "Yasal metin", "Yasal metin bulunamadı.", "Legal document", "Legal document not found."),
        new(IdpMessageKeys.UiTextKeyUnknown, "Ekran yazısı", "Bu anahtarda bir ekran yazısı yok.", "Screen text", "There is no screen text with this key."),
        new(IdpMessageKeys.UiTextInvalid, "Ekran yazısı", "Metin < ya da > içeremez ve varsayılandaki {0} gibi yer tutucuları aynen korumalıdır.", "Screen text", "The text cannot contain < or > and must keep placeholders such as {0} from the default."),
        new(IdpMessageKeys.ManagementPoolUserDeleteNotAllowed, "Kullanıcı", "Panel yöneticileri bu ekrandan silinemez.", "User", "Panel administrators cannot be deleted from this screen."),

        // --- Havuz API'leri ---
        new(IdpMessageKeys.PoolApiCallerNotAllowed, "Yetki", "Bu uç yalnızca havuzun servis hesabı token'ı ile çağrılabilir.", "Authorization", "This endpoint can only be called with a pool service account token."),
        new(IdpMessageKeys.PoolApiRelationRejected, "İlişki", "İlişki havuzun yetki şemasına uymuyor.", "Relation", "The relation does not match the pool's authorization schema."),

        // --- Panelden kullanıcı ekleme / içe aktarma / davet ---
        new(IdpMessageKeys.InvitationInvalid, "Davet", "Bu davet bağlantısı geçersiz ya da süresi dolmuş. Yöneticinizden yeni bir davet isteyin.", "Invitation", "This invitation link is invalid or has expired. Ask your administrator for a new invitation."),
        new(IdpMessageKeys.InvitationNotPending, "Davet", "Bu kullanıcı parolasını zaten belirlemiş; davet gönderilemez.", "Invitation", "This user has already set a password; an invitation cannot be sent."),
        new(IdpMessageKeys.ImportCsvInvalid, "İçe aktarma", "CSV dosyası okunamadı. İlk satır başlık olmalı ve bir email sütunu içermeli (email,ad,soyad).", "Import", "The CSV file could not be read. The first row must be a header containing an email column (email,first_name,last_name)."),
        new(IdpMessageKeys.ImportTooManyRows, "İçe aktarma", "Bir dosyada en fazla {Max} kullanıcı içe aktarılabilir.", "Import", "At most {Max} users can be imported in one file."),
        new(IdpMessageKeys.EmailInvalid, "E-posta", "Geçerli bir e-posta adresi girin.", "Email", "Enter a valid email address."),
        new(IdpMessageKeys.DisplayNameRequired, "Kullanıcı", "Ad soyad zorunludur.", "User", "Display name is required."),

        // --- Servis hesapları ---
        new(IdpMessageKeys.ServiceAccountKeyRequired, "Servis hesabı", "service_account_key ve service_account_secret parametreleri zorunludur.", "Service account", "The service_account_key and service_account_secret parameters are required."),
        new(IdpMessageKeys.InvalidServiceAccount, "Servis hesabı", "Servis hesabı bilgileri hatalı.", "Service account", "The service account credentials are incorrect."),
        new(IdpMessageKeys.ServiceAccountDisabled, "Servis hesabı", "Servis hesabı devre dışı ya da süresi dolmuş.", "Service account", "The service account is disabled or expired."),
    ];
}
