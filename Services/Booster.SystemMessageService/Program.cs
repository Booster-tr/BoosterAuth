using Booster.Auth.Common.Internal.Extensions;
using Booster.SystemMessageService.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSystemMessageServices(builder.Configuration);
builder.Services.AddBoosterApi(builder.Configuration);

var app = builder.Build();

app.UseBoosterServices();
app.UseRouting();
app.UseBoosterCorrelation();
app.UseBoosterApi();

app.Run();
