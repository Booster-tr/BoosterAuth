using Booster.Auth.Idp.Constants;

namespace Booster.Auth.Idp.Models;

/// <summary>
/// Hata ekranının görüntü modeli.
/// <para>
/// Ham <c>error_description</c> asla birincil mesaj olarak gösterilmez:
/// İngilizce, tekniktir ve zaman zaman iç ayrıntı sızdırır. Kullanıcı dostu
/// metin hata kodundan türetilir; ham değerler yalnızca kapalı "Teknik
/// ayrıntılar" bloğunda görünür.
/// </para>
/// </summary>
public sealed class ErrorViewModel
{
    /// <summary>Ham hata kodu (<c>error</c> query parametresi).</summary>
    public string? Error { get; init; }

    /// <summary>Ham açıklama (<c>error_description</c> query parametresi).</summary>
    public string? ErrorDescription { get; init; }

    /// <summary>Varsa, akışa geri dönebilmek için etkileşim anahtarı.</summary>
    public string? InteractionKey { get; init; }

    /// <summary>Destek ekibiyle paylaşılabilecek istek kimliği.</summary>
    public string? RequestId { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    /// <summary>Birincil eylemin etiketi.</summary>
    public required string ActionLabel { get; init; }

    /// <summary>
    /// Birincil eylem, akış devam ettirilebiliyorsa gösterilir.
    /// Etkileşim anahtarı yoksa yeniden denenecek bir şey yoktur.
    /// </summary>
    public bool CanRetry => !string.IsNullOrEmpty(InteractionKey);

    public bool HasTechnicalDetails =>
        !string.IsNullOrEmpty(Error) || !string.IsNullOrEmpty(ErrorDescription) || !string.IsNullOrEmpty(RequestId);

    public static ErrorViewModel Create(string? error, string? description, string? interactionKey, string? requestId)
    {
        var (title, message, actionLabel) = Resolve(error);

        return new ErrorViewModel
        {
            Error = error,
            ErrorDescription = description,
            InteractionKey = interactionKey,
            RequestId = requestId,
            Title = title,
            Message = message,
            ActionLabel = actionLabel
        };
    }

    /// <summary>
    /// Hata kodunu kullanıcı diline çevirir. Hem IdpMessageKeys anahtarları
    /// (<c>idp.*</c>) hem de standart OAuth 2.0 / OIDC hata kodları desteklenir.
    /// </summary>
    private static (string Title, string Message, string ActionLabel) Resolve(string? error) => error switch
    {
        IdpMessageKeys.InvalidInteraction or "invalid_interaction" => (
            "Giriş bağlantısı geçersiz",
            "Bu giriş bağlantısının süresi dolmuş ya da bağlantı artık geçerli değil. Lütfen uygulamaya dönüp yeniden başlayın.",
            "Yeniden başlayın"),

        IdpMessageKeys.SessionInvalid or "session_invalid" => (
            "Oturumunuz sona erdi",
            "Güvenliğiniz için oturumunuz kapatıldı. Devam etmek için yeniden giriş yapın.",
            "Yeniden giriş yapın"),

        IdpMessageKeys.ClientNotFound or "client_not_found" => (
            "Uygulama bulunamadı",
            "Giriş yapmak istediğiniz uygulama tanımlı değil. Lütfen kullandığınız bağlantıyı kontrol edin.",
            "Tekrar deneyin"),

        IdpMessageKeys.ClientInactive or "client_inactive" => (
            "Uygulama şu anda kullanılamıyor",
            "Bu uygulamanın erişimi geçici olarak durduruldu. Lütfen uygulama sahibiyle iletişime geçin.",
            "Tekrar deneyin"),

        IdpMessageKeys.InvalidClient or "invalid_client" => (
            "Uygulama doğrulanamadı",
            "Bu uygulamanın kimliği doğrulanamadı. Sorun devam ederse uygulama sahibine bildirin.",
            "Tekrar deneyin"),

        IdpMessageKeys.InvalidRedirectUri or "invalid_redirect_uri" => (
            "Yönlendirme adresi geçersiz",
            "Güvenliğiniz için bu adrese yönlendirme yapılamıyor. Lütfen durumu uygulama sahibine bildirin.",
            "Tekrar deneyin"),

        IdpMessageKeys.RegistrationNotAllowed or "registration_not_allowed" => (
            "Kayıt oluşturulamıyor",
            "Bu uygulama üzerinden yeni hesap açılmasına izin verilmiyor. Lütfen kurumunuzun yöneticisiyle iletişime geçin.",
            "Giriş sayfasına dönün"),

        IdpMessageKeys.LocalLoginNotAllowed or IdpMessageKeys.ProviderNotAllowed
            or IdpMessageKeys.ProviderNotAvailable => (
            "Bu giriş yöntemi kullanılamıyor",
            "Seçtiğiniz yöntemle giriş yapılmasına bu uygulamada izin verilmiyor. Lütfen başka bir yöntem deneyin.",
            "Giriş sayfasına dönün"),

        "invalid_request" => (
            "İstek tamamlanamadı",
            "Uygulamadan gelen giriş isteği eksik ya da hatalı. Lütfen baştan tekrar deneyin.",
            "Tekrar deneyin"),

        "invalid_scope" => (
            "İstenen izinler geçersiz",
            "Uygulamanın istediği izinlerden biri tanımlı değil. Lütfen durumu uygulama sahibine bildirin.",
            "Tekrar deneyin"),

        "access_denied" => (
            "Erişim izni verilmedi",
            "İşlem iptal edildi ya da bu uygulamaya erişim yetkiniz bulunmuyor.",
            "Tekrar deneyin"),

        "temporarily_unavailable" => (
            "Servis geçici olarak meşgul",
            "Yoğunluk nedeniyle isteğinizi şu anda karşılayamıyoruz. Kısa süre sonra tekrar deneyin.",
            "Tekrar deneyin"),

        "server_error" => (
            "Beklenmeyen bir sorun oluştu",
            "İsteğinizi şu anda tamamlayamadık. Birkaç dakika içinde tekrar denemenizi öneririz.",
            "Tekrar deneyin"),

        _ => (
            "İşlem tamamlanamadı",
            "Beklenmeyen bir durum oluştu. Lütfen tekrar deneyin; sorun sürerse yöneticinizle iletişime geçin.",
            "Tekrar deneyin")
    };
}
