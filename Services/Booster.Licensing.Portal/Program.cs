using Booster.Auth.Common.Internal.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Hashing.Extensions;
using Booster.Hashing.Options;
using Booster.Licensing.Portal.Data;
using Booster.Licensing.Portal.Options;
using Booster.Licensing.Portal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Postgres (licensingdb) + audit interceptor + migration altyapisi; diger servislerle ayni.
builder.Services.AddBoosterServices<LicensingDbContext>(builder.Configuration);

var hashingOptions = builder.Services.AddSettings<HashingOptions>(builder.Configuration);
builder.Services.AddHashing(hashingOptions);

var portal = builder.Services.AddSettings<LicensingPortalOptions>(builder.Configuration);
// Yasal metinlerdeki sirket bilgileri (appsettings "LegalInfo"; dagitimda LegalInfo__CompanyName gibi).
builder.Services.AddSettings<LegalInfo>(builder.Configuration);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SigningKeyStore>();
builder.Services.AddScoped<LicenseIssuer>();
builder.Services.AddScoped<CustomerPortalService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<InstallPackage>();

// Musteri e-postalari Booster Notification servisi uzerinden (Kafka + EF outbox).
builder.Services.AddBoosterMessaging<LicensingDbContext>(builder.Configuration,
    configureRider: rider => Booster.Auth.Common.Internal.Amqp.Extensions.NotificationBusExtensions.AddNotificationProducer(rider));
builder.Services.AddScoped<PortalNotifier>();
builder.Services.AddScoped<RenewalReminderService>();
builder.Services.AddHostedService<RenewalReminderWorker>();

builder.Services.AddDataProtection()
    .SetApplicationName("Booster.Licensing.Portal")
    .PersistKeysToDbContext<LicensingDbContext>();

// Giris Booster Auth ile: musteri havuzu + portal client'i (LicensingBootstrapper).
// Panel gibi PKCE'li authorization code; client secret yok. IdP yalnizca "query" yanit modunu destekler.
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(o =>
    {
        o.Cookie.Name = "booster_licensing";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.AccessDeniedPath = "/yetkisiz";
    })
    .AddOpenIdConnect(o =>
    {
        o.Authority = portal.Authority;
        o.ClientId = portal.ClientId;
        o.ResponseType = OpenIdConnectResponseType.Code;
        o.ResponseMode = OpenIdConnectResponseMode.Query;
        o.UsePkce = true;
        o.RequireHttpsMetadata = portal.RequireHttpsMetadata;
        o.Scope.Clear();
        o.Scope.Add("openid");
        o.Scope.Add("profile");
        o.Scope.Add("email");
        // Talepler oldugu gibi kalsin ("email", "sub"); .NET'in uzun ClaimTypes adlarina cevrilmesin.
        o.MapInboundClaims = false;
        o.TokenValidationParameters.NameClaimType = "name";
        // e-posta id_token'da gelir; userinfo cagrisina gerek yok.
        o.GetClaimsFromUserInfoEndpoint = false;
        o.SaveTokens = false;
        // Booster Auth donusu query ile, ust duzey GET yonlendirmesidir; Lax yeterli. Varsayilan (None + Secure)
        // http uzerinde tarayicilarca dusurulur ve giris "Correlation failed" ile biterdi.
        o.CorrelationCookie.SameSite = SameSiteMode.Lax;
        o.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.NonceCookie.SameSite = SameSiteMode.Lax;
        o.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        // Cikista .NET client_id gondermez (SaveTokens kapali oldugu icin id_token_hint de yok); Booster Auth
        // uygulamayi bilemeyince portala donmez, kullaniciyi kendi "cikis yapildi" ekraninda birakirdi.
        o.Events.OnRedirectToIdentityProviderForSignOut = ctx =>
        {
            ctx.ProtocolMessage.ClientId = portal.ClientId;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    // Musteri alani: e-postasi DOGRULANMIS her kullanici (deneme kurallari e-posta domainine dayanir).
    .AddPolicy(PortalPolicies.Customer, p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => string.Equals(ctx.User.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                                 && ctx.User.FindFirst("sub") is not null))
    .AddPolicy(PortalPolicies.Admin, p => p
        .RequireAuthenticatedUser()
        // E-posta Booster Auth tarafindan DOGRULANMIS olmali (kayitta e-posta kodu); aksi halde biri admin
        // adresiyle kayit olup dogrulamadan giris yapabilirdi.
        .RequireAssertion(ctx => string.Equals(ctx.User.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                                 && portal.IsAdmin(ctx.User.FindFirst("email")?.Value)));

// Tum POST formlari CSRF token ister.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

// Cloudflare tunnel / gateway arkasinda: redirect_uri'nin https ve dogru host ile uretilmesi icin.
// Portal yalnizca cluster icinden (tunnel/gateway) erisilir; proxy adresleri sabit olmadigi icin liste bosaltilir.
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

app.UseExceptionHandler("/hata");
// Govdesiz hata yanitlari (or. 404) bos sayfa yerine ekran gosterir; 404 ziyaretcinin dilinde. Govdesi olan yanitlar etkilenmez.
app.UseStatusCodePagesWithReExecute("/bulunamadi");
app.UseBoosterMessaging();
// AI dosyalari (/llms.txt, /ai/booster-auth/SKILL.md): .md varsayilan olarak sunulmaz.
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".md"] = "text/markdown; charset=utf-8";
contentTypes.Mappings[".txt"] = "text/plain; charset=utf-8";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Panelin "Servis Durumu" ekrani icin surum bilgisi.
app.MapBoosterVersion();
app.MapControllers();

// Sema, anahtar uretiminden ONCE hazir olmali. Diger servisler gibi ApplyMigration bayragina uyulur.
using (var scope = app.Services.CreateScope())
{
    if (scope.ServiceProvider.GetService<Booster.Database.Postgres.Settings.PostgresSettings>() is { ApplyMigration: true })
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().MigrateAsync(app.Lifetime.ApplicationStopping);

    var keys = scope.ServiceProvider.GetRequiredService<SigningKeyStore>();
    await keys.EnsureActiveKeyAsync(app.Lifetime.ApplicationStopping);
    await scope.ServiceProvider.GetRequiredService<OrderService>().SeedPlansAsync(app.Lifetime.ApplicationStopping);
}

app.Run();

public static class PortalPolicies
{
    public const string Admin = "admin";
    public const string Customer = "customer";
}
