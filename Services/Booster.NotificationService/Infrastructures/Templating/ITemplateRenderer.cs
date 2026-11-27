using Booster.Auth.Common.Internal.Notifications;

namespace Booster.NotificationService.Infrastructures.Templating;

public readonly record struct RenderedTemplate(string? Subject, string Body, string LanguageCode);

public interface ITemplateRenderer
{
    /// <summary>
    /// Anahtar + kanal için şablonu bulur ve yer tutucuları doldurur.
    /// Önce havuza özel şablona, yoksa sistem geneli varsayılana bakılır.
    /// Dil için istenen çeviri yoksa varsayılan dile, o da yoksa mevcut ilk çeviriye düşülür.
    /// Şablon hiç yoksa null döner.
    /// </summary>
    Task<RenderedTemplate?> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        Guid userPoolId,
        string languageCode,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct = default);
}
