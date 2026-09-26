using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Stores;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;
using System.Security.Cryptography;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;

/// <summary>
/// Parolasız sağlayıcıların ortak iskeleti: kod üretimi, özetleme, önbellekte
/// tutma, deneme sayma ve tüketme.
/// <para>
/// Bu mekanik e-posta ve telefon için BİREBİR aynıdır; iki sağlayıcıya ayrı
/// ayrı yazılsaydı biri düzeltilip diğeri atlandığında aynı sistemde iki farklı
/// güvenlik davranışı olurdu. Türetilen sınıflar yalnızca farkı söyler:
/// sağlayıcı adı, kabul ettiği kimlik alanı ve normalleştirme kuralı.
/// </para>
/// </summary>
public abstract class PasswordlessProviderBase(
    IProviderPrincipalStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher) : IPasswordlessProvider
{
    /// <summary>
    /// Kısa tutulur: kod posta kutusunda ya da telefonda ne kadar uzun
    /// yaşarsa, o cihaza sonradan erişen biri için o kadar uzun süre
    /// kullanılabilir kalır. Kullanıcıya gösterilen süreyle aynı olmalı.
    /// </summary>
    protected virtual TimeSpan CodeLifetime => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Kaç hatalı denemeden sonra kayıt tamamen silinir. Silinmeseydi altı
    /// haneli kod, kalan ömrü boyunca sınırsız denenebilirdi.
    /// </summary>
    protected virtual int MaxAttempts => 5;

    public abstract string ProviderType { get; }

    public abstract IdentifierType IdentifierType { get; }

    public abstract LoginProviderType LoginProvider { get; }

    public ProviderKind Kind => ProviderKind.Passwordless;

    /// <summary>
    /// Kullanıcının yazdığını arama biçimine çevirir (kırpma, küçük harf,
    /// rakam ayıklama…). Çevrilemiyorsa boş döner ve akış reddedilir.
    /// <para>
    /// <paramref name="config"/> verilir çünkü normalleştirme sağlayıcı
    /// ayarlarına bağlı olabilir (ör. telefon için varsayılan ülke kodu).
    /// </para>
    /// </summary>
    protected abstract string Normalize(string identifier, ProviderConfig config);

    /// <summary>
    /// Kodun gönderileceği adresi kayıttan seçer. Kullanıcının yazdığı değer
    /// DEĞİL, kayıtlı değer kullanılır.
    /// </summary>
    protected abstract string? ResolveTarget(ProviderPrincipal principal);

    public async Task<PasswordlessStartResult> StartAsync(
        PasswordlessStartRequest request,
        ProviderConfig config,
        CancellationToken ct)
    {
        if (!config.IsActive)
            return PasswordlessStartResult.Fail(PasswordlessFailures.ProviderInactive);

        var identifier = Normalize(request.Identifier, config);
        if (string.IsNullOrEmpty(identifier))
            return PasswordlessStartResult.Fail(PasswordlessFailures.InvalidIdentifier);

        var principal = await principalStore.FindByIdentifierAsync(identifier, IdentifierType, ct);

        // Kullanıcı yok ya da kapalı: ayrı sebepler DÖNMEZ, ikisi de aynı
        // sonuçtur. Ayrılsaydı çağıran farklı davranabilir ve uç, hangi
        // adreslerin kayıtlı olduğunu sayan bir araca dönerdi. Çağıranın
        // kullanıcıya ne göstereceği kendi kararıdır.
        if (principal is null || !principal.IsActive)
            return PasswordlessStartResult.Fail(PasswordlessFailures.PrincipalNotFound);

        var target = ResolveTarget(principal);
        if (string.IsNullOrWhiteSpace(target))
            return PasswordlessStartResult.Fail(PasswordlessFailures.NoDeliveryTarget);

        // Süresi dolmamış kod varsa YENİSİ ÜRETİLMEZ.
        //
        // Üretilseydi iki zarar birlikte gelirdi: aynı etkileşimde eski kaydın
        // üzerine yazılır ve kullanıcının kutusundaki kod sessizce geçersizleşir;
        // farklı etkileşimlerde ise her tazelemede yeni bir e-posta/SMS giderdi.
        // Kontrol KULLANICI başına, üretimden ÖNCE yapılır — sonra yapılsaydı
        // kod çoktan yazılmış olurdu.
        var pendingKey = PendingKey(config.IsolationId, principal.Id);
        var pending = await codeStore.GetAsync(pendingKey, ct);

        if (pending is not null && pending.ExpiresAt > DateTimeOffset.UtcNow)
            return PasswordlessStartResult.AlreadySent(principal.Id, target, pending.ExpiresAt);

        var code = GenerateCode();
        var expiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime);

        var record = new PasswordlessCode
        {
            PrincipalId = principal.Id,
            Target = target,
            CodeHash = codeHasher.Hash(code),
            ExpiresAt = expiresAt
        };

        await codeStore.StoreAsync(CacheKey(request.ChallengeKey), record, CodeLifetime, ct);

        // AYNI kayıt ikinci bir anahtarla da yazılır: "bu kullanıcı için kod
        // beklemede" işareti. Kodun kendi kaydı etkileşime bağlı olduğundan,
        // kullanıcı akışı baştan başlatınca o kayıt bulunamaz ve kural boşa
        // çıkardı. Ayrı bir tip gerekmez — anahtarlar farklı, içerik aynı.
        await codeStore.StoreAsync(pendingKey, record, CodeLifetime, ct);

        return PasswordlessStartResult.Success(principal.Id, target, code, expiresAt);
    }

    public async Task<IdentityResult> VerifyAsync(
        PasswordlessVerifyRequest request,
        ProviderConfig config,
        CancellationToken ct)
    {
        var key = CacheKey(request.ChallengeKey);

        // Önce OKU, tüketme: kod hatalı olabilir ve o durumda kaydın silinmesi
        // kullanıcıyı tek yanlış tuşla baştan başlatırdı.
        var record = await codeStore.GetAsync(key, ct);
        if (record is null)
            return IdentityResult.Fail(PasswordlessFailures.CodeInvalid);

        // Geçerlilik kararı kayıttaki mutlak zamandan verilir, önbellek
        // TTL'inden değil: hatalı denemelerde kayıt yeniden yazıldığı için TTL
        // tazelenir, bu alan tazelenmez.
        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await ConsumeAsync(key, config.IsolationId, record.PrincipalId, ct);
            return IdentityResult.Fail(PasswordlessFailures.CodeExpired);
        }

        if (!codeHasher.Verify(request.Code.Trim(), record.CodeHash))
        {
            await RegisterFailedAttemptAsync(key, config.IsolationId, record, ct);
            return IdentityResult.Fail(PasswordlessFailures.CodeInvalid);
        }

        // Doğru kod tek kullanımlıktır; ancak burada tüketilir. Beklemede
        // işareti de silinir: silinmeseydi kullanıcı başarılı girişten sonra
        // kalan süre boyunca yeni kod isteyemezdi.
        await ConsumeAsync(key, config.IsolationId, record.PrincipalId, ct);

        // Kimlik yeniden ARANIR, kayıttaki id'ye güvenilmez: kod üretildikten
        // sonra hesap kapatılmış ya da adres başka bir hesaba taşınmış olabilir.
        // Id karşılaştırması bu ikinci durumu yakalar — yoksa adresi devralan
        // kişi, eski sahibine gönderilmiş kodla giriş yapabilirdi.
        var principal = await principalStore.FindByIdentifierAsync(record.Target, IdentifierType, ct);

        if (principal is null || !principal.IsActive || principal.Id != record.PrincipalId)
            return IdentityResult.Fail(PasswordlessFailures.PrincipalNotFound);

        // Doğru kod, kullanıcının bu adrese eriştiğinin KANITIDIR — alan burada
        // doğrulanmış sayılır. Çağırana bırakılsaydı her parolasız akışta
        // yeniden yazılması gerekir; bir yerde atlandığında kullanıcı zaten
        // kanıtladığı adresi tekrar doğrulamaya çağrılırdı.
        await principalStore.MarkIdentifierVerifiedAsync(principal.Id, IdentifierType, ct);

        return IdentityResult.Success(
            principalId: record.PrincipalId.ToString(),
            providerKey: config.ProviderKey,
            claims: principal.Claims);
    }

    private async Task RegisterFailedAttemptAsync(
        string key, Guid isolationId, PasswordlessCode record, CancellationToken ct)
    {
        if (record.AttemptCount + 1 >= MaxAttempts)
        {
            await ConsumeAsync(key, isolationId, record.PrincipalId, ct);
            return;
        }

        // TTL burada tazelenir ama kaydın ExpiresAt'i DEĞİŞMEZ; geçerlilik o
        // alandan okunduğu için kod deneyerek uzatılamaz.
        await codeStore.StoreAsync(
            key,
            record with { AttemptCount = record.AttemptCount + 1 },
            CodeLifetime,
            ct);
    }

    /// <summary>
    /// Anahtar sağlayıcı adıyla ön eklenir: e-posta ve telefon akışları aynı
    /// etkileşimde birlikte açık olabilir, ön ek olmasaydı biri diğerinin
    /// kodunu ezerdi.
    /// </summary>
    private string CacheKey(string challengeKey) => $"{ProviderType}:{challengeKey}";

    /// <summary>
    /// "Bu kullanıcı için kod beklemede" işaretinin anahtarı. Kullanıcı başına
    /// olmak ZORUNDA: etkileşime bağlı olsaydı kullanıcı sayfayı tazeleyip yeni
    /// etkileşim açarak sınırsız kod isteyebilirdi.
    /// </summary>
    private string PendingKey(Guid isolationId, Guid principalId)
        => $"{ProviderType}:pending:{isolationId}:{principalId}";

    /// <summary>
    /// Talebin İKİ kaydını birlikte siler. Tek yerde toplanır; ayrı ayrı
    /// silinseydi bir dalda beklemede işareti kalır ve kullanıcı kalan süre
    /// boyunca yeni kod isteyemezdi.
    /// </summary>
    private async Task ConsumeAsync(string key, Guid isolationId, Guid principalId, CancellationToken ct)
    {
        await codeStore.RemoveAsync(key, ct);
        await codeStore.RemoveAsync(PendingKey(isolationId, principalId), ct);
    }

    /// <summary>
    /// Altı haneli kod. <c>Random</c> DEĞİL kriptografik üretici kullanılır;
    /// tahmin edilebilir bir kod parolasız girişin tek kapısını açık bırakırdı.
    /// </summary>
    private static string GenerateCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}

/// <summary>
/// Sağlayıcıların döndüğü başarısızlık sebepleri. Çağıran bunları kendi
/// mesaj anahtarlarına çevirir; serbest metin dönülmez ki karşılaştırma
/// yazım hatasına dayanmasın.
/// </summary>
public static class PasswordlessFailures
{
    public const string ProviderInactive = "provider_inactive";
    public const string InvalidIdentifier = "invalid_identifier";
    public const string PrincipalNotFound = "principal_not_found";
    public const string NoDeliveryTarget = "no_delivery_target";
    public const string CodeInvalid = "code_invalid";
    public const string CodeExpired = "code_expired";
}
