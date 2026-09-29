using Booster.Auth.Z.Grpc;
using Booster.Authorization.Fga.Authorization;
using Booster.Authorization.Fga.Handlers;
using Booster.Authorization.Fga.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Authorization.Fga.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, CustomPermissionPolicyProvider>();

        services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
        services.AddSingleton<IAuthorizationHandler, RoleHandler>();

        services.AddScoped<IAuthorizationHandler, FgaHandler>();

        services.AddHttpContextAccessor();

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    public static IServiceCollection AddBoosterAuthZGrpcClient(
        this IServiceCollection services, FgaSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.GrpcAddress);

        services.AddSingleton(settings);

        services
            .AddGrpcClient<AuthorizationService.AuthorizationServiceClient>(options =>
            {
                options.Address = new Uri(settings.GrpcAddress);
            })
            .ConfigureChannel(channel =>
            {
                channel.HttpHandler = new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
                    EnableMultipleHttp2Connections = true
                };
            });

        if (settings.AllowInsecureHttp2)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        return services;
    }

    public static WebApplication UseBoosterAuthorization(this WebApplication app)
    {
        app.UseAuthorization();
        return app;
    }
}
