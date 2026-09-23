using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthNServices(builder.Configuration);

builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

app.UseBoosterServices();
app.UseBoosterApi();

using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ManagementBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.Run();
