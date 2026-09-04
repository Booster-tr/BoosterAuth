using Booster.Database.Postgres.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Booster.Database.Postgres.Extensions;

public static class RepositoryExtension
{
    public static IServiceCollection AddPostgresDbContext<TContext>(
        this IServiceCollection collection,
        PostgresSettings settings,
        bool usePostGIS = false)
        where TContext : DbContext
    {
        collection.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var cs = settings.ConnectionString;
            var builder = new NpgsqlDataSourceBuilder(cs);

            if (usePostGIS)
            {
                builder.UseNetTopologySuite();
            }

            builder.UseJsonNet();

            return builder.Build();
        });

        collection.AddDbContextPool<TContext>((serviceProvider, options) =>
        {
            var dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();

            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                if (usePostGIS)
                    npgsqlOptions.UseNetTopologySuite();
            });

            var interceptors = serviceProvider.GetServices<IInterceptor>();
            options.AddInterceptors(interceptors);
        });

        return collection;
    }
}