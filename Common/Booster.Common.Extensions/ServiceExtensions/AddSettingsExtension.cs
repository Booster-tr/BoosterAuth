using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Booster.Common.Extensions.ServiceExtensions;

public static class ServiceBaseExtensions
{
    public static T AddSettings<T>(this IServiceCollection services, IConfiguration configuration)
    where T : class, new()
    {
        var section = configuration.GetSection(typeof(T).Name);

        if (!section.Exists())
        {
            throw new InvalidOperationException($"Configuration section '{typeof(T).Name}' not found.");
        }

        var settings = section.Get<T>() ?? throw new InvalidOperationException($"Failed to bind configuration section '{typeof(T).Name}' to '{typeof(T)}'.");
        services.Configure<T>(section);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<T>>().Value);

        return settings;
    }
}
