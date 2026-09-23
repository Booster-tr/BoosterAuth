using Booster.Auth.Database.DbContextBase;
using Booster.Auth.Manager.RequestContexts;
using Booster.LoggingService.Features.Logs._base.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Booster.LoggingService.Features._base;

public class LoggerDbContext(DbContextOptions<LoggerDbContext> opt, IRequestContextAccessor requestContextAccessor) : PostgresDbContext(opt, requestContextAccessor)
{
    public DbSet<Log> Logs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
