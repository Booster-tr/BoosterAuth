using MassTransit;
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

        // MassTransit outbox/inbox tablolari. Log ve audit mesajlari is
        // islemiyle AYNI transaction'da outbox'a yazilir: broker o an kapaliysa
        // bile kayit kaybolmaz, baglanti gelince teslim edilir.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
