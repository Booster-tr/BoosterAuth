using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Booster.Labs.Site.Controllers;

/// <summary>
/// Kurumsal tanitim sitesi (tr / en). Adresler: /{dil}, /{dil}/products, /{dil}/products/boosters-auth,
/// /{dil}/games, /{dil}/games/block-boosters, /{dil}/about, /{dil}/contact. Sayfalar sunucuda uretilir (SEO).
/// </summary>
public sealed class SiteController(SiteOptions options) : Controller
{
    // Kok: tarayici diline gore tr/en (302).
    [HttpGet("/")]
    public IActionResult Root() => Redirect($"/{SiteCatalog.PreferredLanguage(Request.Headers.AcceptLanguage)}");

    [HttpGet("/{lang:regex(^(tr|en)$)}")]
    public IActionResult Home(string lang) => Render(lang, SiteCatalog.Home);

    [HttpGet("/{lang:regex(^(tr|en)$)}/{**slug}")]
    public IActionResult Page(string lang, string slug)
    {
        var page = SiteCatalog.Find(slug.Trim('/'));
        return page is null ? NotFound() : Render(lang, page);
    }

    private IActionResult Render(string lang, SiteCatalog.Page page) =>
        View($"~/Views/Site/{lang}/{page.View}.cshtml", new SitePageModel(lang, page));

    // ---------------------------------------------------------------- hata / bulunamadi
    [HttpGet("/bulunamadi")]
    public IActionResult NotFoundPage()
    {
        var lang = SiteCatalog.PreferredLanguage(Request.Headers.AcceptLanguage);
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View($"~/Views/Site/{lang}/notfound.cshtml", new SitePageModel(lang, SiteCatalog.Home));
    }

    // ---------------------------------------------------------------- SEO
    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content(
        $"User-agent: *\nAllow: /\n\nSitemap: {BaseUrl}/sitemap.xml\n", "text/plain", Encoding.UTF8);

    [HttpGet("/sitemap.xml")]
    public IActionResult Sitemap()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n");
        foreach (var page in SiteCatalog.All)
            foreach (var lang in SiteCatalog.Languages)
            {
                sb.Append("  <url>\n    <loc>").Append(BaseUrl).Append(page.Path(lang)).Append("</loc>\n");
                foreach (var alt in SiteCatalog.Languages)
                    sb.Append("    <xhtml:link rel=\"alternate\" hreflang=\"").Append(alt).Append("\" href=\"").Append(BaseUrl).Append(page.Path(alt)).Append("\"/>\n");
                sb.Append("    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"").Append(BaseUrl).Append(page.Path(SiteCatalog.DefaultLanguage)).Append("\"/>\n");
                sb.Append("    <changefreq>weekly</changefreq>\n    <priority>").Append(page == SiteCatalog.Home ? "1.0" : "0.7").Append("</priority>\n  </url>\n");
            }
        sb.Append("</urlset>\n");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private string BaseUrl => options.PublicUrl.TrimEnd('/');
}
