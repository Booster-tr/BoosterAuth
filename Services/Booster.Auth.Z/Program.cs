using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Z.Bootstrappers;
using Booster.Auth.Z.Extensions;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Relational.Extensions;
using Booster.Authorization.Relational.Options;
using Booster.Common.Extensions.ServiceExtensions;
using FastEndpoints;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

var fgaOptions = builder.Services.AddSettings<FgaOptions>(builder.Configuration);
builder.Services.AddBoosterAuthZ(fgaOptions);

builder.Services.AddAuthZServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

builder.Services.AddGrpc();

var app = builder.Build();

// Gateway "/authz" on ekini SILMEZ (AuthZ rotalari "authz/..." ile baslar); bu yuzden public
// https://auth.../authz/version istegi buraya "/authz/version" olarak gelir. Surum ucu "/version"da (ortak).
// Routing'den ONCE yeniden yazilir; cluster ici "/version" (panel Servis Durumu) aynen calisir.
app.UseRewriter(new RewriteOptions().AddRewrite("^authz/version$", "version", skipRemainingRules: true));

app.UseBoosterServices();
app.UseBoosterCorrelation();
app.UseBoosterApi(wrapResponses: true);

app.MapGrpcService<AuthorizationGrpcService>();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ManagementBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);

    // Lisans portali yalnizca Booster'in kendi kurulumunda acilir (Management.Api ile ayni bayrak).
    if (builder.Configuration.GetValue<bool>("BootstrapOptions:Licensing:Enabled"))
    {
        var licensingBootstrapper = scope.ServiceProvider.GetRequiredService<LicensingBootstrapper>();
        await licensingBootstrapper.InitializeAsync(CancellationToken.None);
    }
}

app.Run();
