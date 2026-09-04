using Booster.ExceptionManager.Abstraction.Translations;
using Booster.ExceptionManager.WithCacheProvider.Translations;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.ExceptionManager.WithCacheProvider.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterCacheLanguageTranslator(this IServiceCollection services)
    {
        services.AddScoped<ITranslationManager, TranslationManager>();
        return services;
    }
}
