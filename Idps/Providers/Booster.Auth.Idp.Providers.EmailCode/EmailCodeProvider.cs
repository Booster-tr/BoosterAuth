using Booster.Auth.Database.Models.Enums;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Models;
using Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Stores;
using Booster.Caching.Abstraction;
using Booster.Hashing.OneWays.Simple;

namespace Booster.Auth.Idp.Providers.EmailCode;

/// <summary>
/// E-posta adresine gönderilen tek kullanımlık kodla parolasız giriş.
/// <para>
/// Kod üretimi, saklama, deneme sayma ve tüketme
/// <see cref="PasswordlessProviderBase"/>'te; burada yalnızca e-postaya özgü
/// olan üç şey var: sağlayıcı adı, kabul edilen kimlik alanı ve adresin
/// normalleştirilmesi.
/// </para>
/// </summary>
public sealed class EmailCodeProvider(
    IProviderPrincipalStore principalStore,
    ISecureCacheStore<PasswordlessCode> codeStore,
    ISimpleHasher codeHasher)
    : PasswordlessProviderBase(principalStore, codeStore, codeHasher)
{
    /// <summary>
    /// Havuz politikasındaki <see cref="LoginProviderType.EmailCode"/> ile
    /// eşleşen ad. İstek bu adı taşır, fabrika bu adla çözer.
    /// </summary>
    public const string Type = "email_code";

    public override string ProviderType => Type;

    public override IdentifierType IdentifierType => IdentifierType.Email;

    public override LoginProviderType LoginProvider => LoginProviderType.EmailCode;

    /// <summary>
    /// E-postalar küçük harfe indirilir: kayıt sırasında da öyle yazıldığı için
    /// (bkz. RegisterHandler) aksi hâlde "Ad@Ornek.com" yazan kullanıcı kendi
    /// hesabını bulamazdı.
    /// </summary>
    protected override string Normalize(string identifier, ProviderConfig config)
        => identifier.Trim().ToLowerInvariant();

    protected override string? ResolveTarget(ProviderPrincipal principal)
        => principal.Email;
}
