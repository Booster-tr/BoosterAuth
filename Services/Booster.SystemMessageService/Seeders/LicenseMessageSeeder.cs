using Booster.Auth.Common.Internal.Constants.Messages;
using Booster.SystemMessageService.Seeders._base;

namespace Booster.SystemMessageService.Seeders;

/// <summary>Lisans mesajlari (LICENSE_*). Anahtar ayni zamanda istemciye donen hata kodudur.</summary>
public class LicenseMessageSeeder(IServiceScopeFactory scopeFactory, ILogger<LicenseMessageSeeder> logger)
    : SystemMessageSeederBase(scopeFactory, logger)
{
    protected override IReadOnlyList<Msg> Messages =>
    [
        new(LicenseMessageKeys.Restricted, "Lisans",
            "Lisansınız geçerli değil ya da süresi doldu. Yönetim paneli salt okunur; Sistem > Lisans ekranından lisansınızı yenileyin.",
            "License",
            "Your license is not valid or has expired. The management panel is read-only; renew your license from System > License."),

        // Son kullaniciya teknik ayrinti GOSTERILMEZ.
        new(LicenseMessageKeys.ServiceUnavailable, "Hizmet",
            "Hizmet şu anda kullanılamıyor. Lütfen daha sonra tekrar deneyin.",
            "Service",
            "The service is currently unavailable. Please try again later."),

        new(LicenseMessageKeys.InvalidToken, "Lisans",
            "Lisans anahtarı geçersiz. Lütfen lisans portalından aldığınız anahtarın tamamını yapıştırın.",
            "License",
            "The license key is invalid. Please paste the full key you received from the license portal."),
        new(LicenseMessageKeys.UnknownKey, "Lisans",
            "Bu lisans anahtarı bu sürüm tarafından tanınmıyor. Ürünü güncelleyin ya da lisans portalından yeni bir anahtar alın.",
            "License",
            "This license key is not recognized by this version. Update the product or get a new key from the license portal."),
        new(LicenseMessageKeys.InstallationMismatch, "Lisans",
            "Bu lisans başka bir kurulum için üretilmiş. Lisansı bu ekrandaki kurulum kimliği (GUID) ile alın.",
            "License",
            "This license was issued for a different installation. Get the license for the installation ID (GUID) shown on this screen."),
        new(LicenseMessageKeys.UnsupportedVersion, "Lisans",
            "Bu lisans biçimi bu sürümde desteklenmiyor. Ürünü güncelleyin.",
            "License",
            "This license format is not supported by this version. Update the product."),
        new(LicenseMessageKeys.Expired, "Lisans",
            "Bu lisansın süresi (ek süre dahil) dolmuş. Lisans portalından lisansınızı yenileyin.",
            "License",
            "This license has expired (including the grace period). Renew your license from the license portal."),
    ];
}
