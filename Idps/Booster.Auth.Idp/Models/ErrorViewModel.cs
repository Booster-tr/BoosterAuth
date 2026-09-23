using Booster.Auth.Idp.Constants;

namespace Booster.Auth.Idp.Models;

public sealed class ErrorViewModel
{
    public string? Error { get; init; }

    public string? ErrorDescription { get; init; }

    public string? InteractionKey { get; init; }

    public string? RequestId { get; init; }

    public required string Title { get; init; }

    public required string Message { get; init; }

    public required string ActionLabel { get; init; }

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

    private static (string Title, string Message, string ActionLabel) Resolve(string? error) => error switch
    {
        IdpMessageKeys.InvalidInteraction or "invalid_interaction" => (
            "Giriş bağlantısı geçersiz",
            "Bu giriş bağlantısının süresi dolmuş ya da bağlantı artık geçerli değil. Lütfen uygulamaya dönüp yeniden başlayın.",
            "Yeniden başlayın"),

        IdpMessageKeys.LoggedOut or "logged_out" => (
            "Oturumunuz kapatıldı",
            "Güvenle çıkış yaptınız. Devam etmek için uygulamaya dönüp yeniden giriş yapabilirsiniz.",
            "Giriş sayfasına dönün"),

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
