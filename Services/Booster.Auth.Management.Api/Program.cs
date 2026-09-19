using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Extensions;
using FastEndpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthNServices(builder.Configuration);

builder.Services.AddFastEndpoints();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseFastEndpoints(c => c.Endpoints.Configurator = ep => ep.AllowAnonymous());

// Management kayıtları (tenant, havuz, client, admin rolü) her açılışta
// kontrol edilir; yalnızca ilk seferde yazılır.
using (var scope = app.Services.CreateScope())
{
    var bootstrapper = scope.ServiceProvider.GetRequiredService<ManagementBootstrapper>();
    await bootstrapper.InitializeAsync(CancellationToken.None);
}

app.Run();
