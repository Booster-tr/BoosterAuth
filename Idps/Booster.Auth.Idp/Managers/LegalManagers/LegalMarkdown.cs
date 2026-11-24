using System.Net;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Booster.Auth.Idp.Managers.LegalManagers;

/// <summary>
/// Yasal metinlerin Markdown -&gt; HTML donusumu. Metni panel yoneticisi yazar ama giris sayfasinda (IdP alan adinda)
/// gosterilir; bu yuzden ham HTML ISLENMEZ ve yalnizca http(s) / mailto / tel / sayfa ici baglantilara izin verilir
/// (javascript: vb. atilir). Basliklar (##) icindekiler listesini olusturur.
/// </summary>
public static class LegalMarkdown
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAutoIdentifiers()
        .UsePipeTables()
        .Build();

    public sealed record Heading(string Id, string Text);

    public sealed record Rendered(string Html, IReadOnlyList<Heading> Headings);

    /// <param name="placeholders">{{anahtar}} -&gt; deger; degerler HTML-encode edilerek yerlestirilir.</param>
    public static Rendered Render(string markdown, IReadOnlyDictionary<string, string> placeholders)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
                link.Url = "#";
        }
        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (!IsSafeUrl(autolink.Url))
                autolink.Url = "#";
        }

        var headings = document.Descendants<HeadingBlock>()
            .Where(h => h.Level == 2)
            .Select(h => new Heading(h.GetAttributes().Id ?? string.Empty, InlineText(h.Inline)))
            .Where(h => h.Id.Length > 0)
            .ToList();

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        writer.Flush();

        // Yer tutucular HTML'e donusumden SONRA ve encode edilerek yerlestirilir: deger Markdown/HTML olarak yorumlanmaz.
        var html = writer.ToString();
        foreach (var (key, value) in placeholders)
            html = html.Replace("{{" + key + "}}", WebUtility.HtmlEncode(value), StringComparison.Ordinal);

        return new Rendered(html, headings
            .Select(h => h with { Text = ReplacePlain(h.Text, placeholders) })
            .ToList());
    }

    public static string ReplacePlain(string text, IReadOnlyDictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            text = text.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        return text;
    }

    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        url = url.Trim();
        if (url.StartsWith('#') || url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal))
            return true;
        return url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase);
    }

    private static string InlineText(ContainerInline? inline)
    {
        if (inline is null)
            return string.Empty;
        var parts = inline.Descendants<LiteralInline>().Select(l => l.Content.ToString());
        return string.Concat(parts).Trim();
    }
}
