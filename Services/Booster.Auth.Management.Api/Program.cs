using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Extensions;
using Booster.Auth.Management.Api.Features.Clients.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthNServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

builder.Services.AddGrpc();

var app = builder.Build();

app.UseBoosterServices();
app.UseBoosterMessaging();
app.UseBoosterApi(wrapResponses: true);

app.MapGrpcService<ClientDataGrpcService>();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ManagementBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);

    var walkToWinBootstrapper = scope.ServiceProvider.GetRequiredService<WalkToWinBootstrapper>();
    await walkToWinBootstrapper.InitializeAsync(CancellationToken.None);

    // Yalnizca BootstrapOptions.Licensing.Enabled ise (Booster'in kendi kurulumu) bir sey yapar.
    var licensingBootstrapper = scope.ServiceProvider.GetRequiredService<LicensingBootstrapper>();
    await licensingBootstrapper.InitializeAsync(CancellationToken.None);
}

app.Run();
