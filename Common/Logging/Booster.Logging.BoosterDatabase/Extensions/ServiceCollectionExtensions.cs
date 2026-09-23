using Booster.Logging.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Logging.BoosterDatabase.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoosterDatabaseLogging(this IServiceCollection services, Action<DatabaseLoggingOptions> configure)
    {
        var options = new DatabaseLoggingOptions();
        configure(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<DatabaseLogQueue>();
        services.AddSingleton<ILogWriter, DatabaseLogWriter>();
        services.AddHostedService<DatabaseLogBackgroundService>();
        return services;
    }
}
