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

app.Run();
