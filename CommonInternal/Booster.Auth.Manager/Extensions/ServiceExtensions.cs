using Booster.Auth.Manager.Middlewares;
using Booster.Auth.Manager.Options;
using Booster.Auth.Manager.RequestContexts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Booster.Auth.Manager.Extensions;


public static class ServiceExtensions
{

    public static IServiceCollection AddBoosterAuthentication(this IServiceCollection services, ApiAuthOptions authOptions)
    {

        services.AddSingleton<IRequestContextAccessor, RequestContextAccessor>();

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authBuilder.AddJwtBearer(options =>
        {
            //claimleri bizim verdiğimiz ad ile bırakır.
            options.MapInboundClaims = false;

            options.Authority = authOptions.Authority;

            options.RequireHttpsMetadata = authOptions.Authority.StartsWith("https");

            options.Audience = authOptions.Audience;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidIssuer = authOptions.Authority,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "sub",
                RoleClaimType = "role"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

     
        return services;

    }

    public static WebApplication UseBoosterAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseMiddleware<RequestContextMiddleware>();
        return app;
    }
}

