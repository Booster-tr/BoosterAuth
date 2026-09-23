using Booster.Auth.Common.Internal.Extensions;
using Booster.NotificationService.Extensions;
using Booster.NotificationService.Features._base;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotificationServices(builder.Configuration);
builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

app.UseBoosterServices();
app.UseRouting();
app.UseBoosterMessaging();
app.UseBoosterApi();

// Şablonlar olmadan hiçbir bildirim render edilemez; servis açılışında kurulur.
using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<NotificationBootstrapper>();
    await bootstrapper.RunAsync();
}

app.Run();
