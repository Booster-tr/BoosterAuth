using Booster.Caching.Abstraction;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Booster.Caching.Memory.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Salt-bellek (L1) kurar ve <see cref="ICacheService"/>'i <see cref="MemoryFusionCacheService"/>
    /// ile kaydeder.
    /// </summary>
    public static IServiceCollection AddBoosterMemoryCache(this IServiceCollection services, Action<MemoryCacheOptions>? configure = null)
    {
        var options = new MemoryCacheOptions();
        configure?.Invoke(options);

        services.AddMemoryCache(mem =>
        {
            mem.SizeLimit = options.SizeLimit;
            mem.CompactionPercentage = 0.20;
        });

        services.AddFusionCache()
                .WithDefaultEntryOptions(entry =>
                {
                    entry.Duration = options.DefaultDuration;
                    entry.IsFailSafeEnabled = true;
                    entry.FailSafeMaxDuration = options.FailSafeMaxDuration;
                    entry.Size = 1;
                })
                .WithMemoryCache(sp => sp.GetRequiredService<IMemoryCache>());

        services.AddSingleton<ICacheService, MemoryFusionCacheService>();
        services.AddSingleton(typeof(ISecureCacheStore<>), typeof(SecureCacheStore<>));

        return services;
    }
}
