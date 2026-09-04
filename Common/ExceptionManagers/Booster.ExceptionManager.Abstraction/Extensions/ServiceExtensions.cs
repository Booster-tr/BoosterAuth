using Booster.ExceptionManager.Abstraction.Middlewares;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.ExceptionManager.Abstraction.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterExceptionHandler(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static WebApplication UseBoosterExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
