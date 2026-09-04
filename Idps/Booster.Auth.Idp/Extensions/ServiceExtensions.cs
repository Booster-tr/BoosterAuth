using Booster.Auth.Idp.Providers.Local.Stores;
using Booster.Auth.Idp.Stores.ClientStore;
using Booster.Auth.Idp.Stores.PrincipalStores;
using Booster.Auth.Idp.Stores.SessionStores;

namespace Booster.Auth.Idp.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddIdpServices(this IServiceCollection services)
    {
        services.AddScoped<IClientDataStore, MockClientDataStore>();
        services.AddScoped<ILocalUserStore, PrincipalStore>();
        services.AddScoped<ISessionStore, SessionStore>();
        
        return services;
    }
}
