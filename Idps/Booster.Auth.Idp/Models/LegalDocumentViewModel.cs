using Booster.Auth.Idp.Managers.LegalManagers;

namespace Booster.Auth.Idp.Models;

/// <summary>
/// Yasal metin sayfasi. Metin uygulama sahibinindir ve veritabanindadir (LegalDocument, panelden duzenlenir);
/// <see cref="LegalPage.Html"/> guvenli Markdown donusumunden gelir (LegalMarkdown: ham HTML islenmez).
/// </summary>
public sealed record LegalDocumentViewModel(LegalPage Page, string? ReturnUrl);
