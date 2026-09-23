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
    ];
}
