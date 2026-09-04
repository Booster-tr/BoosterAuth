using Booster.ExceptionManager.Abstraction.Translations;
using Booster.ExceptionManager.LocalDict.Translations;
using Microsoft.Extensions.DependencyInjection;

namespace Booster.ExceptionManager.LocalDict.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddBoosterExceptionTranslator(this IServiceCollection services)
    {
        services.AddScoped<ITranslationManager, TranslationContext>();
        return services;
    }
}
