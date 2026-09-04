using Booster.Auth.Database.Constants;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace Booster.Auth.Database.DbContextBase;


public class PostgresDbContext(DbContextOptions options, IRequestContextAccessor _requestContextAccessor) : DbContext(options)
{
    protected Guid IsolationId => _requestContextAccessor.Context().Isolation;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isolatedMethod = typeof(PostgresDbContext).GetMethod(
            nameof(ConfigureIsolationFilter), BindingFlags.NonPublic | BindingFlags.Instance);

        var softDeleteMethod = typeof(PostgresDbContext).GetMethod(
            nameof(ConfigureSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;

            if (typeof(IIsolatedEntity).IsAssignableFrom(clrType))
            {
                isolatedMethod!.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
                continue;
            }
            if (typeof(IEntity).IsAssignableFrom(clrType))
                softDeleteMethod!.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
        }
    }

    protected void ConfigureIsolationFilter<TEntity>(ModelBuilder builder) where TEntity : class, IIsolatedEntity
    {
        builder.Entity<TEntity>().Property(e => e.IsolationId).IsRequired();
        builder.Entity<TEntity>().Property(e => e.IsolationId).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

        builder.Entity<TEntity>().HasQueryFilter(e =>
            (e.IsolationId == IsolationId) &&
            EF.Property<DateTimeOffset?>(e, BasePropertyNames.DeletedTimePropName) == null);
    }

    protected void ConfigureSoftDeleteFilter<TEntity>(ModelBuilder builder) where TEntity : class, IEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            EF.Property<DateTimeOffset?>(e, BasePropertyNames.DeletedTimePropName) == null);
    }
}
