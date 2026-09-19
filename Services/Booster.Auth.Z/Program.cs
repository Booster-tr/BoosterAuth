using Booster.Auth.Manager.Extensions;
using Booster.Auth.Manager.Options;
using Booster.Auth.Z.Extensions;
using Booster.Authorization.Relational.Extensions;
using Booster.Authorization.Relational.Options;
using Booster.Common.Extensions.ServiceExtensions;

var builder = WebApplication.CreateBuilder(args);

var fgaSettings = builder.Services.AddSettings<FgaOptions>(builder.Configuration);
var apiAuthOptions = builder.Services.AddSettings<ApiAuthOptions>(builder.Configuration);

builder.Services.AddBoosterAuthentication(apiAuthOptions); 
builder.Services.AddBoosterAuthZ(fgaSettings);

builder.Services.AddAuthZServices(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/", () => "Hello World!");

app.Run();
