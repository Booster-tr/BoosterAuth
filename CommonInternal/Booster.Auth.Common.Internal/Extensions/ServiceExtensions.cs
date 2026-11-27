using Microsoft.AspNetCore.HttpOverrides;
using Booster.Auth.Common.Internal.Authorization;
using Microsoft.AspNetCore.Authorization;
using Booster.Auth.Common.Internal.Correlation;
using Booster.Auth.Manager.Extensions;
using Booster.Auth.Manager.Options;
using Booster.Auth.Manager.RequestContexts;
using Booster.Authorization.Fga.Extensions;
using Booster.Authorization.Fga.Options;
using Booster.Caching.Redis;
using Booster.Auth.Common.Internal.Models;
using Booster.Auth.Common.Internal.Licensing;
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
using Booster.Observability;
using Booster.Auth.Common.Internal.Auditing;
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

        // Log + trace + metrik (OpenTelemetry). OTLP adresi yoksa yalnizca konsol (bkz. Booster.Observability).
        services.AddBoosterObservability(configuration,
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "booster");
        services.AddHttpContextAccessor();
        services.AddSingleton<AuditLog>();

        return services;
    }

    public static WebApplication UseBoosterServices(this WebApplication app)
    {
        // Gateway istemcinin gercek IP'sini ve https bilgisini X-Forwarded-* ile iletir (YarpGateway ClientIpResolver;
        // istemcinin yazdigi basligi ezer). Arka servisler yalnizca ic agdan (gateway) erisilir: ozel ag
        // araliklarina guvenilir. Bu olmadan oturumlara gateway'in IP'si yaziliyor, HSTS de hic eklenmiyordu.
        var forwarded = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        foreach (var network in new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "127.0.0.0/8", "::1/128", "fc00::/7" })
            forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        app.UseForwardedHeaders(forwarded);

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
        // Panel uclari yalnizca yonetim havuzu token'ini kabul eder (bkz. PanelActorHandler). FGA handler'indan ONCE
        // kaydedilir ve ret sonrasi diger handler'lar calismaz: yoksa FGA, musterinin deposunda olmayan tipi sorup
        // 403 yerine 500 donuyordu.
        services.AddSingleton<IAuthorizationHandler, PanelActorHandler>();
        services.Configure<AuthorizationOptions>(o => o.InvokeHandlersAfterFailure = false);
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

        // Panel / servis hesabi yazma islemleri ve kisisel veri okumalari denetim kaydina (AdminAuditMiddleware).
        app.UseMiddleware<AdminAuditMiddleware>();

        app.UseFastEndpoints(c =>
        {
            // Lisans kisitliyken panel yazma isteklerini reddeder; son kullanici akislarina dokunmaz (LicenseGuard).
            c.Endpoints.Configurator = ep => ep.PreProcessor<LicenseGuard>(Order.Before);

            // Dogrulama hatalari her serviste istisna isleyicisinden gecer: mesaj anahtari istegin diline cevrilir ve
            // ayni bicimde doner. Once yalnizca sarmalanan servislerde boyleydi; IdP ham anahtari FastEndpoints
            // biciminde donduruyor, panel de kullaniciya "idp.email_invalid" gosteriyordu.
            c.Errors.ResponseBuilder = (failures, _, _) => throw new ValidationException(failures);

            if (!wrapResponses) return;

            c.Serializer.ResponseSerializer = (rsp, dto, contentType, jsonContext, ct) =>
            {
                rsp.ContentType = contentType;
                var envelope = ApiResponse.Ok(dto);
                return rsp.WriteAsJsonAsync(envelope, cancellationToken: ct);
            };
        });

        return app;
    }

    /// <summary>Istege correlation id verir (hata loglari ve yanit basligi: X-Correlation-ID).</summary>
    public static WebApplication UseBoosterCorrelation(this WebApplication app)
    {
        app.UseMiddleware<CorrelationMiddleware>();

        return app;
    }
}
