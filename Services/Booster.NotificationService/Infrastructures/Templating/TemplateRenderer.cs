using Booster.Database.Repositories;
using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Common.Internal.Helpers;
using Booster.NotificationService.Constants;
using Booster.NotificationService.Features._base;
using Booster.NotificationService.Features._base.Entities;
using Microsoft.EntityFrameworkCore;

namespace Booster.NotificationService.Infrastructures.Templating;

public class TemplateRenderer(
    IAsyncRepository<NotificationTemplate, NotificationDbContext> templateRepository) : ITemplateRenderer
{
    private const string PlaceholderPattern = @"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}";

    public async Task<RenderedTemplate?> RenderAsync(
        string templateKey,
        NotificationChannel channel,
        Guid userPoolId,
        string languageCode,
        IReadOnlyDictionary<string, string> data,
        CancellationToken ct = default)
    {
        var template = await PickTemplateAsync(templateKey, channel, userPoolId, ct);
        if (template is null)
            return null;

        var translation = PickTranslation(template, languageCode);
        if (translation is null)
            return null;

        return new RenderedTemplate(
            translation.Subject is null ? null : Fill(translation.Subject, data),
            Fill(translation.Body, data),
            translation.LanguageCode);
    }

    private async Task<NotificationTemplate?> PickTemplateAsync(
        string templateKey, NotificationChannel channel, Guid userPoolId, CancellationToken ct)
    {
        var template = await templateRepository.GetAsync(t => t.Key == templateKey && t.Channel == channel && t.IsActive, include: w => w.Include(r => r.Translations), cancellationToken: ct);

        return template;
    }

    private static NotificationTemplateTranslation? PickTranslation(NotificationTemplate template, string languageCode)
    {
        var requested = template.Translations
            .FirstOrDefault(t => string.Equals(t.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));
        if (requested is not null)
            return requested;

        var fallback = template.Translations
            .FirstOrDefault(t => string.Equals(t.LanguageCode, NotificationConstants.DefaultLanguage, StringComparison.OrdinalIgnoreCase));

        return fallback ?? template.Translations.FirstOrDefault();
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string> data)
        => SafeRegex.Replace(text, PlaceholderPattern, match =>
            data.TryGetValue(match.Groups["key"].Value, out var value) ? value : string.Empty);
}
