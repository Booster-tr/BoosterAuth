using Booster.Authorization.Relational.Extensions;
using Booster.Authorization.Relational.Options;
using Booster.Common.Extensions.ServiceExtensions;

var builder = WebApplication.CreateBuilder(args);

var fgaSettings = builder.Services.AddSettings<FgaOptions>(builder.Configuration);

builder.Services.AddBoosterAuthZ(fgaSettings);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/", () => "Hello World!");

app.Run();
