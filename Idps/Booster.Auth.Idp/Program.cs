using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Idp.Bootstrappers;
using Booster.Auth.Idp.Extensions;
using Booster.Auth.Idp.Features.Principals.Grpc;
using Booster.Auth.Idp.Localization;
using Microsoft.AspNetCore.Localization;

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

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseBoosterServices();

app.UseRequestLocalization();

app.UseRouting();

app.UseBoosterMessaging();
app.UseBoosterApi();

app.MapStaticAssets().AllowAnonymous();

app.MapGrpcService<PrincipalLookupGrpcService>();
app.MapGrpcService<PrincipalAccessGrpcService>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<AdminBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.Run();
