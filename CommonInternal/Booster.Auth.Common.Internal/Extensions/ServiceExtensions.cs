using Booster.Auth.Manager.Extensions;
using Booster.Auth.Manager.Options;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Extensions;
using Booster.Authorization.Fga.Options;
using Booster.Caching.Redis;
using FastEndpoints;
using Booster.Caching.Redis.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Auth.Database.Interceptors;
using Booster.Database.Interceptors;
using Booster.Database.Postgres.Extensions;
using Booster.Database.Postgres.Settings;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Booster.Auth.Common.Internal.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterServices<TContext>(
        this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        var postgresSettings = services.AddSettings<PostgresSettings>(configuration);
        var redisOptions = services.AddSettings<RedisCacheOptions>(configuration);

        services.AddSingleton<IInterceptor, AuditInterceptor>();
        services.AddSingleton<IInterceptor, IsolationInterceptor>();

        services.AddPostgresDbContext<TContext>(postgresSettings);
        services.AddScoped(typeof(IAsyncRepository<,>), typeof(RepositoryBase<,>));

        services.TryAddSingleton<IRequestContextAccessor, RequestContextAccessor>();

        services.AddBoosterRedisCache(redisOptions);

        services.AddProblemDetails();

        return services;
    }

    public static WebApplication UseBoosterServices(this WebApplication app)
    {
        app.UseBoosterExceptionHandler();
        app.UseHttpsRedirection();

        return app;
    }

    public static IServiceCollection AddBoosterApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var apiAuthOptions = services.AddSettings<ApiAuthOptions>(configuration);
        var fgaSettings = services.AddSettings<FgaSettings>(configuration);

        services.AddBoosterAuthentication(apiAuthOptions);

        services.AddBoosterAuthZGrpcClient(fgaSettings);
        services.AddBoosterAuthorization();

        services.AddFastEndpoints();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }

    public static WebApplication UseBoosterApi(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseBoosterAuthentication();
        app.UseBoosterAuthorization();

        app.UseFastEndpoints();

        return app;
    }
}
