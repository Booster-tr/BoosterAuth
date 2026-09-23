using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Microsoft.EntityFrameworkCore;

namespace Booster.NotificationService.Features._base;

// EF Core design-time (migrations) için factory. Runtime'da DI kullanılır.
public class NotificationDbContextFactory : BaseDbContextFactory<NotificationDbContext>
{
    protected override NotificationDbContext CreateNewInstance(DbContextOptions<NotificationDbContext> options)
    {
        return new NotificationDbContext(options, new DummyRequestContextAccessor());
    }
}
