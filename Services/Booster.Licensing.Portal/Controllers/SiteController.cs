using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Booster.Licensing.Portal.Controllers;

/// <summary>Urun tanitim sitesi: paketler ve kurulum paketi indirme. Herkese acik.</summary>
[AllowAnonymous]
public sealed class SiteController(LicensingDbContext db, InstallPackage package) : Controller
{
    [HttpGet("/paketler")]
    public async Task<IActionResult> Packages(CancellationToken ct) =>
        View(await db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync(ct));

    [HttpGet("/indir")]
    public IActionResult Download() => View(InstallPackage.Files);

    [HttpGet("/indir/" + InstallPackage.FileName)]
    public IActionResult DownloadZip()
    {
        Response.Headers.CacheControl = "no-cache";
        return File(package.Zip, "application/zip", InstallPackage.FileName);
    }
}
