using Booster.Caching.Abstraction;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Booster.Caching.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Redis destekli FusionCache (L1 bellek + L2 Redis distributed + backplane + fail-safe) kurar ve
    /// <see cref="ICacheService"/>'i <see cref="FusionCacheService"/> ile kaydeder. <see cref="IConnectionMultiplexer"/>
    /// de singleton olarak açığa çıkarılır (raw Redis erişimi gereken yerler için).
    /// </summary>
    public static IServiceCollection AddBoosterRedisCache(this IServiceCollection services, Action<RedisCacheOptions> configure)
    {
        var options = new RedisCacheOptions();
        configure(options);
        return services.AddBoosterRedisCache(options);
    }

    public static IServiceCollection AddBoosterRedisCache(this IServiceCollection services, RedisCacheOptions options)
    {
        services.AddSingleton(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Endpoints);

        services.AddMemoryCache(mem =>
        {
            mem.SizeLimit = options.SizeLimit;
            mem.CompactionPercentage = 0.20;
        });

        var isDebugging = System.Diagnostics.Debugger.IsAttached;
        var factoryTimeout = isDebugging ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(2);
        var distributedTimeout = isDebugging ? TimeSpan.FromMinutes(1) : TimeSpan.FromMilliseconds(300);

        var redisConfig = new ConfigurationOptions
        {
            Password = options.Password,
            ConnectTimeout = isDebugging ? 30000 : options.ConnectTimeout,
            SyncTimeout = isDebugging ? 30000 : options.SyncTimeout,
            AbortOnConnectFail = options.AbortOnConnectFail,
            KeepAlive = 60,
            Ssl = options.Ssl,
            SslProtocols = options.Ssl ? SslProtocols.Tls12 : SslProtocols.None
        };
        foreach (var endpoint in options.Endpoints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            redisConfig.EndPoints.Add(endpoint);

        services.AddStackExchangeRedisCache(o =>
        {
            o.ConfigurationOptions = redisConfig;
            o.InstanceName = options.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfig));

        services.AddFusionCache()
                .WithDefaultEntryOptions(entry =>
                {
                    entry.Duration = options.DefaultDuration;

                    entry.IsFailSafeEnabled = true;
                    entry.FailSafeMaxDuration = options.FailSafeMaxDuration;

                    entry.FactorySoftTimeout = factoryTimeout.Divide(10);
                    entry.FactoryHardTimeout = factoryTimeout;

                    entry.DistributedCacheSoftTimeout = distributedTimeout;
                    entry.DistributedCacheHardTimeout = distributedTimeout.Multiply(5);

                    entry.JitterMaxDuration = TimeSpan.FromSeconds(10);

                    entry.Size = 1;
                })
                .WithMemoryCache(sp => sp.GetRequiredService<IMemoryCache>())
                .WithDistributedCache(sp => sp.GetRequiredService<IDistributedCache>())
                .WithSerializer(new FusionCacheSystemTextJsonSerializer(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }))
                .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
                {
                    ConfigurationOptions = redisConfig
                }));

        services.AddSingleton<ICacheService, FusionCacheService>();
        services.AddSingleton(typeof(ISecureCacheStore<>), typeof(SecureCacheStore<>));

        return services;
    }
}
