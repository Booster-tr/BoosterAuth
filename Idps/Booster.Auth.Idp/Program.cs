using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Idp.Bootstrappers;
using Booster.Auth.Idp.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddIdpServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseBoosterServices();

app.UseRouting();

app.UseBoosterMessaging();
app.UseBoosterApi();

app.MapStaticAssets().AllowAnonymous();

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
