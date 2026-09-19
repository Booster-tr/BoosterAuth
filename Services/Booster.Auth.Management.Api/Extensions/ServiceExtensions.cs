using Booster.Auth.Management.Api.Features._base;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Redis;
using Booster.Caching.Redis.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Database.Postgres.Extensions;
using Booster.Database.Postgres.Settings;
using Booster.Database.Repositories;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Booster.Auth.Management.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthNServices(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresSettings = services.AddSettings<PostgresSettings>(configuration);
        var redisOptions = services.AddSettings<RedisCacheOptions>(configuration);

        // PostgresDbContext izolasyon erişimcisine bağımlıdır. Bu serviste izolasyon
        // filtresi kullanılmıyor (entity'ler Entity'den türüyor) ama base tipin
        // bağımlılığı yine de karşılanmalı.
        services.TryAddSingleton<IRequestContextAccessor, RequestContextAccessor>();

        services.AddPostgresDbContext<AuthNDbContext>(postgresSettings);
        services.AddScoped(typeof(IAsyncRepository<,>), typeof(RepositoryBase<,>));

        services.AddBoosterRedisCache(redisOptions);

        return services;
    }
}
