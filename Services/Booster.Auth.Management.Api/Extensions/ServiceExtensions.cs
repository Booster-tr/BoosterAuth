using Booster.Auth.Common.Internal.Extensions;
using Booster.Auth.Common.Internal.Options;
using Booster.Auth.Management.Api.Bootstrappers;
using Booster.Auth.Management.Api.Features._base;
using Booster.Common.Extensions.ServiceExtensions;

namespace Booster.Auth.Management.Api.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddAuthNServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBoosterServices<AuthNDbContext>(configuration);
        services.AddSettings<BootstrapOptions>(configuration);

        services.AddScoped<ManagementBootstrapper>();

        return services;
    }
}
