using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Microsoft.EntityFrameworkCore;

namespace Booster.SystemMessageService.Features._base;

// EF Core design-time (migrations) için factory. Runtime'da DI kullanılır.
public class SystemMessageDbContextFactory : BaseDbContextFactory<SystemMessageDbContext>
{
    protected override SystemMessageDbContext CreateNewInstance(DbContextOptions<SystemMessageDbContext> options)
    {
        return new SystemMessageDbContext(options, new DummyRequestContextAccessor());
    }
}
