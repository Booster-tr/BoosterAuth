using Booster.Auth.Common.Internal.Extensions;
using Booster.LoggingService.Extensions;
using Booster.LoggingService.Features._base;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoggingServices(builder.Configuration);
builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

app.UseBoosterServices();
app.UseRouting();
app.UseBoosterMessaging();
app.UseBoosterApi();

app.Run();
