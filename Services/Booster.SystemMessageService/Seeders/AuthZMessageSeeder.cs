using Booster.SystemMessageService.Seeders._base;

namespace Booster.SystemMessageService.Seeders;

/// <summary>Yetkilendirme servisinin (<c>authz.*</c>) mesajları.</summary>
public class AuthZMessageSeeder(IServiceScopeFactory scopeFactory, ILogger<AuthZMessageSeeder> logger)
    : SystemMessageSeederBase(scopeFactory, logger)
{
    protected override IReadOnlyList<Msg> Messages =>
    [
        new(AuthZMessageKeys.PoolStoreNotFound, "Yetkilendirme", "Bu havuz için yetkilendirme deposu tanımlı değil.", "Authorization", "No authorization store is defined for this pool."),
        new(AuthZMessageKeys.PoolStoreAlreadyExists, "Yetkilendirme", "Bu havuzun yetkilendirme deposu zaten var.", "Authorization", "An authorization store already exists for this pool."),
        new(AuthZMessageKeys.SchemaNotFound, "Yetkilendirme", "Yetki şeması bulunamadı.", "Authorization", "Authorization schema not found."),
        new(AuthZMessageKeys.ResourceTypeNotFound, "Yetkilendirme", "Kaynak tipi şemada tanımlı değil.", "Authorization", "The resource type is not defined in the schema."),
        new(AuthZMessageKeys.InvalidObjectFormat, "Yetkilendirme", "Geçersiz nesne biçimi. Beklenen biçim: tip:kimlik.", "Authorization", "Invalid object format. Expected: type:id."),
        new(AuthZMessageKeys.InvalidUserFormat, "Yetkilendirme", "Geçersiz özne biçimi. Beklenen biçim: user:kimlik.", "Authorization", "Invalid subject format. Expected: user:id."),
        new(AuthZMessageKeys.InvalidCorrelationId, "Yetkilendirme", "Eşleştirme kimliği yalnızca harf, rakam, alt çizgi ve tire içerebilir; en fazla 36 karakter olabilir.", "Authorization", "The correlation id may contain only letters, digits, underscore and hyphen, and at most 36 characters."),
        new(AuthZMessageKeys.DuplicateCorrelationId, "Yetkilendirme", "Eşleştirme kimlikleri birbirinden farklı olmalıdır.", "Authorization", "Correlation ids must be unique."),
        new(AuthZMessageKeys.SchemaConflict, "Yetkilendirme", "Şema siz düzenlerken başka biri tarafından değiştirildi. Sayfayı yenileyip tekrar deneyin.", "Authorization", "The schema was changed by someone else while you were editing. Refresh and try again."),
        new(AuthZMessageKeys.SchemaHasIssues, "Yetkilendirme", "Sürümde çözülmemiş sorunlar var; yayına almadan önce düzeltin.", "Authorization", "The version has unresolved issues; fix them before activating it."),
        new(AuthZMessageKeys.SchemaRejected, "Yetkilendirme", "Şema yetkilendirme motoru tarafından reddedildi. İlişkilerin birbirine bağlanışını kontrol edin.", "Authorization", "The authorization engine rejected the schema. Check how the relations reference each other."),
        new(AuthZMessageKeys.SchemaVersionNotFound, "Yetkilendirme", "Şema sürümü bulunamadı.", "Authorization", "Schema version not found."),
        new(AuthZMessageKeys.SchemaVersionActiveReadOnly, "Yetkilendirme", "Yayındaki sürüm düzenlenemez. Değiştirmek için kopyasından yeni bir sürüm oluşturun.", "Authorization", "The live version cannot be edited. Create a new version from a copy to change it."),
        new(AuthZMessageKeys.SchemaVersionActiveCannotDelete, "Yetkilendirme", "Yayındaki sürüm silinemez. Önce başka bir sürümü yayına alın.", "Authorization", "The live version cannot be deleted. Activate another version first."),
        new(AuthZMessageKeys.SchemaVersionNameTaken, "Yetkilendirme", "Bu adla başka bir sürüm var.", "Authorization", "Another version already uses this name."),
        new(AuthZMessageKeys.SchemaLimitExceeded, "Yetkilendirme", "Kaynak tipi ya da tip başına yetki sınırı aşıldı.", "Authorization", "The resource type or permissions-per-type limit was exceeded."),
        new(AuthZMessageKeys.SchemaVersionLimitReached, "Yetkilendirme", "Sürüm sınırına ulaşıldı. Yeni sürüm için kullanılmayan bir sürümü silin.", "Authorization", "The version limit was reached. Delete an unused version to create a new one."),
        new(AuthZMessageKeys.PoolRelationNotAllowed, "Yetkilendirme", "Yayındaki şema bu ilişkiye izin vermiyor: tip, yetki ya da atanan taraf uyuşmuyor.", "Authorization", "The live schema does not allow this relation: the type, permission or assignee does not match."),
        new(AuthZMessageKeys.QueryNotAllowed, "Yetkilendirme", "Bu havuzun yetkilerini sorgulama izniniz yok. Token'ı bu havuz vermiş olmalı ya da havuzda görüntüleme yetkiniz olmalı.", "Authorization", "You may not query this pool's permissions. The token must be issued by this pool, or you must have view access to the pool."),
        new(AuthZMessageKeys.PoolRelationRejected, "Yetkilendirme", "İlişki yazılamadı; zaten var olabilir.", "Authorization", "The relation could not be written; it may already exist."),
        new(AuthZMessageKeys.PoolRelationNotFound, "Yetkilendirme", "Silinecek ilişki bulunamadı.", "Authorization", "The relation to delete was not found."),
    ];
}
