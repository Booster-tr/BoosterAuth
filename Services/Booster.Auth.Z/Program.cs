using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Z.Bootstrappers;
using Booster.Auth.Z.Extensions;
using Booster.Auth.Z.Grpc;
using Booster.Authorization.Relational.Extensions;
using Booster.Authorization.Relational.Options;
using Booster.Common.Extensions.ServiceExtensions;
using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);

var fgaOptions = builder.Services.AddSettings<FgaOptions>(builder.Configuration);
builder.Services.AddBoosterAuthZ(fgaOptions);

builder.Services.AddAuthZServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

builder.Services.AddGrpc();
builder.Services.AddControllers();

var app = builder.Build();

app.UseBoosterServices();
app.UseBoosterMessaging();
app.UseBoosterApi(wrapResponses: true);

app.MapGrpcService<AuthorizationGrpcService>();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ManagementBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.Run();
