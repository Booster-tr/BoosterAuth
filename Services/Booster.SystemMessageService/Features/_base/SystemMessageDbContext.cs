using Booster.Amqp.Outbox;
using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.SystemMessageService.Features.SystemMessages._base.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.SystemMessageService.Features._base;

public class SystemMessageDbContext(DbContextOptions<SystemMessageDbContext> opt, IRequestContextAccessor requestContextAccessor) : PostgresDbContext(opt, requestContextAccessor)
{
    public DbSet<SystemMessage> SystemMessages { get; set; }
    public DbSet<SystemMessageTranslation> SystemMessageTranslations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.AddBoosterOutbox();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
