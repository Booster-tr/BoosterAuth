using Booster.Labs.Site;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Salt tanitim sitesi: veritabani / kimlik yok. Yalnizca MVC (sunucuda uretilen sayfalar, SEO).
builder.Services.AddControllersWithViews();

var siteOptions = builder.Configuration.GetSection("SiteOptions").Get<SiteOptions>() ?? new SiteOptions();
builder.Services.AddSingleton(siteOptions);

var app = builder.Build();

// Cloudflare tunnel arkasinda: canonical/sitemap adreslerinin https + dogru host uretilmesi icin.
// Proxy adresleri sabit olmadigindan liste bosaltilir (yalnizca cluster icinden erisim).
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

// Guvenlik basliklari (IdP ile ayni posture): CSP 'self' (satir ici style/script YOK), nosniff, Referrer-Policy.
// HSTS yalnizca https'te (X-Forwarded-Proto). form-action YOK: iletisim formu kendi adresine POST eder.
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
        "font-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["X-Frame-Options"] = "DENY";
    if (ctx.Request.IsHttps)
        h["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

app.UseStatusCodePagesWithReExecute("/bulunamadi");

// .webmanifest dogru tiple sunulsun.
var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

app.UseRouting();
app.MapControllers();

app.Run();
