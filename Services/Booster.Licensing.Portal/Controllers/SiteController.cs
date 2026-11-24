using System.Text;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Data.Entities;
using Booster.Licensing.Portal.Options;
using Booster.Licensing.Portal.Services;
using Booster.Licensing.Portal.Site;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Controllers;

/// <summary>Gorunumlere giden ortak model.</summary>
public sealed record SitePageModel(string Lang, SiteCatalog.Page Page)
{
    public IReadOnlyList<Plan> Plans { get; init; } = [];
    public IReadOnlyList<string> PackageFiles { get; init; } = [];
    public string T(string key) => SiteCatalog.T(Lang, key);
    public string Href(string slug) => slug.Length == 0 ? $"/{Lang}" : $"/{Lang}/{slug}";
}

/// <summary>
/// Urun tanitim sitesi (tr / en). Adresler: /{dil}, /{dil}/features, /{dil}/pricing, /{dil}/download, /{dil}/docs/...
/// Sayfalar sunucuda uretilir (SEO); kayit: <see cref="SiteCatalog"/>.
/// </summary>
[AllowAnonymous]
public sealed class SiteController(LicensingDbContext db, InstallPackage package, LicensingPortalOptions options) : Controller
{
    [HttpGet("/{lang:regex(^(tr|en)$)}")]
    public Task<IActionResult> Home(string lang, CancellationToken ct) => Render(lang, SiteCatalog.Home, ct);

    [HttpGet("/{lang:regex(^(tr|en)$)}/{**slug}")]
    public Task<IActionResult> Page(string lang, string slug, CancellationToken ct)
    {
        var page = SiteCatalog.Find(slug.Trim('/'));
        return page is null ? Task.FromResult<IActionResult>(NotFound()) : Render(lang, page, ct);
    }

    private async Task<IActionResult> Render(string lang, SiteCatalog.Page page, CancellationToken ct)
    {
        var model = new SitePageModel(lang, page);
        if (page == SiteCatalog.Pricing || page == SiteCatalog.Home)
            model = model with { Plans = await db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(ct) };
        if (page == SiteCatalog.Download)
            model = model with { PackageFiles = InstallPackage.Files(lang) };
        return View($"~/Views/Site/{lang}/{page.View}.cshtml", model);
    }

    // ---------------------------------------------------------------- eski adresler (kalici yonlendirme)
    [HttpGet("/paketler")]
    public IActionResult OldPricing() => RedirectPermanent("/tr/pricing");

    [HttpGet("/indir")]
    public IActionResult OldDownload() => RedirectPermanent("/tr/download");

    // ---------------------------------------------------------------- kurulum paketi
    /// <summary>Paket sitenin o anki dilinde (betikler, README, yorumlar).</summary>
    [HttpGet("/{lang:regex(^(tr|en)$)}/download/" + InstallPackage.FileName)]
    public IActionResult DownloadZip(string lang)
    {
        Response.Headers.CacheControl = "no-cache";
        return File(package.Zip(lang), "application/zip", InstallPackage.FileName);
    }

    // Eski adresler: /indir Turkce sayfadan geliyordu; /download tarayici diline gore.
    [HttpGet("/indir/" + InstallPackage.FileName)]
    public IActionResult OldDownloadZipTr() => DownloadZip("tr");

    [HttpGet("/download/" + InstallPackage.FileName)]
    public IActionResult OldDownloadZip() => DownloadZip(SiteCatalog.PreferredLanguage(Request.Headers.AcceptLanguage));

    // ---------------------------------------------------------------- SEO
    [HttpGet("/robots.txt")]
    public IActionResult Robots() => Content(
        $"User-agent: *\nAllow: /\nDisallow: /hesap\nDisallow: /admin\nDisallow: /giris\nDisallow: /signin-oidc\n\nSitemap: {BaseUrl}/sitemap.xml\n",
        "text/plain", Encoding.UTF8);

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
                sb.Append("    <changefreq>weekly</changefreq>\n    <priority>").Append(page == SiteCatalog.Home ? "1.0" : page.DocGroup is null ? "0.8" : "0.6").Append("</priority>\n  </url>\n");
            }
        sb.Append("</urlset>\n");
        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    private string BaseUrl => options.PublicUrl.TrimEnd('/');
}
