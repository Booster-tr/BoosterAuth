using Booster.Amqp.Constants;
using Booster.Amqp.Extensions;
using Booster.Auth.Manager.Extensions;
using Booster.Auth.Manager.Options;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Extensions;
using Booster.Authorization.Fga.Options;
using Booster.Caching.Redis;
using Booster.Auth.Common.Internal.Models;
using FastEndpoints;
using FluentValidation;
using Booster.Caching.Redis.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Auth.Database.Interceptors;
using Booster.Database.Interceptors;
using Booster.Database.Postgres.Extensions;
using Booster.Database.Postgres.Settings;
using Booster.Database.Repositories;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.Logging.BoosterAmqp.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        services.AddScoped<IDatabaseMigrator, DatabaseMigrator<TContext>>();
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

        // Panelin "Servis Durumu" ekrani icin surum / ayakta mi bilgisi.
        app.MapBoosterVersion();

        // Bootstrapper'lar Program.cs'te bu çağrıdan SONRA çalışır; şemanın
        // onlardan önce hazır olması gerekir.
        if (app.Services.GetService<PostgresSettings>() is { ApplyMigration: true })
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetService<IDatabaseMigrator>()
                ?.MigrateAsync(app.Lifetime.ApplicationStopping)
                .GetAwaiter().GetResult();
        }

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

    public static WebApplication UseBoosterApi(this WebApplication app, bool wrapResponses = false)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseBoosterAuthentication();
        app.UseBoosterAuthorization();

        app.UseFastEndpoints(c =>
        {
            if (!wrapResponses) return;

            c.Errors.ResponseBuilder = (failures, _, _) => throw new ValidationException(failures);

            c.Serializer.ResponseSerializer = (rsp, dto, contentType, jsonContext, ct) =>
            {
                rsp.ContentType = contentType;
                var envelope = ApiResponse.Ok(dto);
                return rsp.WriteAsJsonAsync(envelope, cancellationToken: ct);
            };
        });

        return app;
    }


    public static IServiceCollection AddBoosterMessaging<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IRiderRegistrationConfigurator>? configureRider = null,
        Action<IKafkaFactoryConfigurator, IRiderRegistrationContext>? configureKafka = null)
        where TContext : DbContext
    {
        var amqpSettings = services.AddSettings<AmqpSettings>(configuration);

        services.AddBoosterCorrelationContext();
        services.AddAmqpLogger();

        services.AddKafkaBus<TContext>(
            amqpSettings,
            configureConsumers,
            rider =>
            {
                rider.AddLogProducer();
                configureRider?.Invoke(rider);
            },
            configureKafka);

        return services;
    }

    public static WebApplication UseBoosterMessaging(this WebApplication app)
    {
        app.UseBoosterCorrelationContext();

        return app;
    }
}
