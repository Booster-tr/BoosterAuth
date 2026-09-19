using Booster.Auth.Z.Features._base;
using Booster.Auth.Z.Stores.PoolStores;
using Booster.Caching.Redis;
using Booster.Caching.Redis.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.Database.Postgres.Extensions;
using Booster.Database.Postgres.Settings;
using Booster.Database.Repositories;

namespace Booster.Auth.Z.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthZServices(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresSettings = services.AddSettings<PostgresSettings>(configuration);
        var redisOptions = services.AddSettings<RedisCacheOptions>(configuration);

        services.AddPostgresDbContext<AuthZDbContext>(postgresSettings);
        services.AddScoped(typeof(IAsyncRepository<,>), typeof(RepositoryBase<,>));

        services.AddBoosterRedisCache(redisOptions);

        services.AddScoped<IPoolStoreProvider, PoolStoreProvider>();

        return services;
    }
}
