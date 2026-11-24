using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Idp.Bootstrappers;
using Booster.Auth.Idp.Extensions;
using Booster.Auth.Idp.Features.Principals.Grpc;
using Booster.Auth.Idp.Localization;
using Booster.Auth.Idp.Stores.ClientLogoStores;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddViewOptions(options => options.HtmlHelperOptions.ClientValidationEnabled = false);

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    string[] supported = ["tr-TR", "en-US"];

    options.SetDefaultCulture(supported[0]);
    options.AddSupportedCultures(supported);
    options.AddSupportedUICultures(supported);

    // Sıra: ÖNCE ÇEREZ, sonra Accept-Language başlığı.
    //
    // Çerez, dil seçicisinden gelen AÇIK tercihtir; başlık ise tarayıcının
    // varsayılanı. Başlık önce okunsaydı seçici hiç çalışmazdı: kullanıcı dili
    // değiştirir, çerez yazılır, ama her istekte başlık onu ezerdi.
    options.RequestCultureProviders =
    [
        new AcceptLanguageCookieProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddIdpServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

builder.Services.AddGrpc();

var app = builder.Build();

app.UseBoosterServices();

// UseBoosterServices X-Forwarded-Proto'yu uyguladiktan SONRA: istek https gorunmezse HSTS eklenmez.
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Guvenlik basliklari (tum IdP yanitlari). CSP: sayfalar yalnizca IdP'nin kendi script / stil / resimlerini yukler;
// yasal metne gomulu dis resim (takip pikseli) ya da araya sizan bir script calismaz. form-action YOK: giris formu
// uygulamanin redirect_uri'sine (baska origin) 302 ile gider, form-action bunu engellerdi.
app.Use((context, next) =>
{
    var headers = context.Response.Headers;
    // Swagger UI satir ici script kullanir; CSP onu bozmasin.
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
            "object-src 'none'; base-uri 'self'; frame-ancestors 'self'";
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    return next(context);
});

app.UseRequestLocalization();

// Uygulama logoları çalışma anında yüklenir; MapStaticAssets yalnızca derleme
// anında bilinen dosyaları servis ettiği için bu klasör ayrıca açılır.
var logoStore = app.Services.GetRequiredService<IClientLogoStore>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(logoStore.Directory),
    RequestPath = $"/{ClientLogoStore.FolderName}",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
        // Adres sürüm damgası taşır (?v=); logo değişince adres de değişir.
        ctx.Context.Response.Headers.CacheControl = "public, max-age=86400";
    }
});

app.UseRouting();

app.UseBoosterMessaging();
app.UseBoosterApi();

app.MapStaticAssets().AllowAnonymous();

app.MapGrpcService<PrincipalLookupGrpcService>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);

    await scope.ServiceProvider.GetRequiredService<LegalBootstrapper>().InitializeAsync(CancellationToken.None);
}

app.Run();
