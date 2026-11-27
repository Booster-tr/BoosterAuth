using Booster.Auth.Common.Internal.Extensions;
using Booster.NotificationService.Extensions;
using Booster.NotificationService.Features._base;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotificationServices(builder.Configuration);
builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

app.UseBoosterServices();
app.UseRouting();
app.UseBoosterCorrelation();
app.UseBoosterApi();

// IdP ve lisans portalinin bildirim istekleri (ic ag, gRPC portu 8081).
app.MapGrpcService<Booster.NotificationService.Features.Dispatch.NotificationDispatchGrpcService>();

// Şablonlar olmadan hiçbir bildirim render edilemez; servis açılışında kurulur.
using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<NotificationBootstrapper>();
    await bootstrapper.RunAsync();
}

app.Run();
