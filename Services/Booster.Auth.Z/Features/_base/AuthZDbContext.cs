using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.Auth.Z.Features._base.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.Auth.Z.Features._base;

public class AuthZDbContext(
    DbContextOptions<AuthZDbContext> options,
    IRequestContextAccessor requestContextAccessor)
    : PostgresDbContext(options, requestContextAccessor)
{
    public DbSet<PoolSchema> PoolSchemas => Set<PoolSchema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
