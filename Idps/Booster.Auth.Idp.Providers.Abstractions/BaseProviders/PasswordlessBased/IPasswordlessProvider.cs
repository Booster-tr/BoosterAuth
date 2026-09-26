using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;

/// <summary>
/// Parolasız giriş sağlayıcısı: kullanıcı parola bilmez, eriştiği bir kanala
/// gönderilen tek kullanımlık kodu girer.
/// <para>
/// Akış İKİ adımdır; <c>ICredentialProvider</c>'daki tek adımlı doğrulama
/// burada işlemez çünkü arada kodun teslim edilmesi gerekir.
/// </para>
/// </summary>
public interface IPasswordlessProvider : IIdentityProvider
{
    /// <summary>
    /// Bu sağlayıcının hangi kimlik alanını kabul ettiği. Havuz politikası bu
    /// alana izin vermiyorsa sağlayıcı hiç çalıştırılmamalı.
    /// </summary>
    IdentifierType IdentifierType { get; }

    /// <summary>
    /// Kodu üretir, özetini saklar ve teslim bilgisini döner. Gönderim çağıranın
    /// sorumluluğundadır.
    /// </summary>
    Task<PasswordlessStartResult> StartAsync(
        PasswordlessStartRequest request,
        ProviderConfig config,
        CancellationToken ct);

    /// <summary>
    /// Girilen kodu doğrular. Başarılıysa kayıt tüketilir; kod ikinci kez
    /// kullanılamaz.
    /// </summary>
    Task<IdentityResult> VerifyAsync(
        PasswordlessVerifyRequest request,
        ProviderConfig config,
        CancellationToken ct);
}
