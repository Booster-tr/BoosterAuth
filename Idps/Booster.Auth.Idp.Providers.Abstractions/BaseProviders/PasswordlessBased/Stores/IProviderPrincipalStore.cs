using Booster.Auth.Database.Models.Enums;

namespace Booster.Auth.Idp.Providers.Abstractions.BaseProviders.PasswordlessBased.Stores;

/// <summary>
/// Sağlayıcıların kullanıcıyı tek bir kimlik alanına göre aradığı kapı.
/// <para>
/// <c>ILocalUserStore</c> kullanılamaz: o arama kullanıcının parola kaydı
/// olmasını şart koşar. Parolasız kullanıcının parolası hiç olmayabilir, o
/// arama onu hiçbir zaman bulamazdı.
/// </para>
/// <para>
/// Uygulaması IdP projesindedir; sağlayıcı projeleri veritabanını görmez.
/// </para>
/// </summary>
public interface IProviderPrincipalStore
{
    Task<ProviderPrincipal?> FindByIdentifierAsync(
        string identifier,
        IdentifierType identifierType,
        CancellationToken ct);

    /// <summary>
    /// Kimlik alanını doğrulanmış olarak işaretler.
    /// <para>
    /// Parolasız girişte kodu doğru girmek, kullanıcının o adrese eriştiğinin
    /// KANITIDIR — ayrı bir doğrulama turu istemek aynı ispatı ikinci kez
    /// sormak olurdu. Zaten doğrulanmışsa yazma yapılmaz: parolasız giriş her
    /// oturumda tekrarlanır, her seferinde yazmak gereksiz yazma ve önbellek
    /// geçersizleştirmesi demekti.
    /// </para>
    /// </summary>
    Task MarkIdentifierVerifiedAsync(
        Guid principalId,
        IdentifierType identifierType,
        CancellationToken ct);
}

/// <summary>Sağlayıcının kullanıcı hakkında görmesi gereken kadarı.</summary>
public sealed record ProviderPrincipal
{
    public required Guid Id { get; init; }

    public required bool IsActive { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public IReadOnlyDictionary<string, string> Claims { get; init; }
        = new Dictionary<string, string>();
}
