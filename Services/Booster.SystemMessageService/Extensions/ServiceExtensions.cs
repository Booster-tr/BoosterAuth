using Booster.Auth.Common.Internal.Extensions;
using Booster.Common.Extensions.ServiceExtensions;
using Booster.ExceptionManager.Abstraction.Extensions;
using Booster.ExceptionManager.WithCacheProvider.Extensions;
using Booster.SystemMessageService.Features._base;
using Booster.SystemMessageService.Features.SystemMessages.RefreshCache;
using Booster.SystemMessageService.Seeders;

namespace Booster.SystemMessageService.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddSystemMessageServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<SystemMessageDbContext>(configuration);

        services.AddBoosterCacheLanguageTranslator();
        services.AddBoosterExceptionHandler();

        services.AddScoped<RefreshCacheSystemMessagesEventHandler>();

        services.AddHostedService<AuthNMessageSeeder>();
        services.AddHostedService<LicenseMessageSeeder>();
        services.AddHostedService<AuthZMessageSeeder>();
        services.AddHostedService<IdpMessageSeeder>();
        services.AddHostedService<CommonMessageSeeder>();
        services.AddHostedService<CacheSeederService>();

        return services;
    }

}
