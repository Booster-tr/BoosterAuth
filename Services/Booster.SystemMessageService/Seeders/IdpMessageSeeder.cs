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
        new(IdpMessageKeys.UpscopingNotAllowed, "Yetki", "Var olandan daha geniş yetki istenemez.", "Scope", "You cannot request broader scopes than already granted."),
        new(IdpMessageKeys.RefreshTokenReuseDetected, "Oturum", "Yenileme jetonu tekrar kullanıldı; güvenlik için tüm oturum kapatıldı.", "Session", "The refresh token was reused; the whole session was revoked for safety."),
        new(IdpMessageKeys.RefreshTokenExpired, "Oturum", "Yenileme jetonunun süresi doldu. Lütfen yeniden giriş yapın.", "Session", "The refresh token has expired. Please sign in again."),
        new(IdpMessageKeys.SessionInvalid, "Oturum", "Oturumunuz geçerli değil. Lütfen yeniden giriş yapın.", "Session", "Your session is not valid. Please sign in again."),
        new(IdpMessageKeys.InvalidAccessToken, "Token", "Erişim jetonu geçersiz.", "Token", "The access token is invalid."),
        new(IdpMessageKeys.InvalidSubjectToken, "Token", "Takas edilecek jeton geçersiz.", "Token", "The subject token is invalid."),
        new(IdpMessageKeys.ResourceRequiredForExchange, "Token", "Jeton takası için hedef kaynak belirtilmelidir.", "Token", "A target resource is required for token exchange."),
        new(IdpMessageKeys.AudienceNotAllowed, "Token", "Bu hedef kitle uygulama için tanımlı değil.", "Token", "This audience is not allowed for the application."),

        // --- Servis hesapları ---
        new(IdpMessageKeys.ServiceAccountKeyRequired, "Servis hesabı", "service_account_key ve service_account_secret parametreleri zorunludur.", "Service account", "The service_account_key and service_account_secret parameters are required."),
        new(IdpMessageKeys.InvalidServiceAccount, "Servis hesabı", "Servis hesabı bilgileri hatalı.", "Service account", "The service account credentials are incorrect."),
        new(IdpMessageKeys.ServiceAccountDisabled, "Servis hesabı", "Servis hesabı devre dışı ya da süresi dolmuş.", "Service account", "The service account is disabled or expired."),
    ];
}
