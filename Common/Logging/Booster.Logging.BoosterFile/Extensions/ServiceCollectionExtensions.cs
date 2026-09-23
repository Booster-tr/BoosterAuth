using Booster.Logging.Abstraction;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.Logging.BoosterFile.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBoosterFileLogging(this IServiceCollection services, Action<FileLoggingOptions>? configure = null)
    {
        var options = new FileLoggingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<FileLogQueue>();
        services.AddSingleton<ILogWriter, FileLogWriter>();
        services.AddHostedService<FileLogBackgroundService>();
        return services;
    }
}
