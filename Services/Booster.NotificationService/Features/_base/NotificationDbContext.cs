using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.NotificationService.Features._base.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.NotificationService.Features._base;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> opt, IRequestContextAccessor requestContextAccessor)
    : PostgresDbContext(opt, requestContextAccessor)
{
    public DbSet<NotificationTemplate> NotificationTemplates { get; set; }
    public DbSet<NotificationTemplateTranslation> NotificationTemplateTranslations { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
