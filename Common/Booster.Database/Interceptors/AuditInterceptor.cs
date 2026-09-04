using Booster.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;

namespace Booster.Database.Interceptors;

public class AuditInterceptor : SaveChangesInterceptor
{
    private static readonly ConcurrentDictionary<Type, List<string>> _recursiveCascadeCache = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null)
        {
            await ProcessAudit(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context != null)
        {
            ProcessAudit(eventData.Context).GetAwaiter().GetResult();
        }

        return base.SavingChanges(eventData, result);
    }

    private async Task ProcessAudit(DbContext context, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entries = context.ChangeTracker.Entries<IEntity>().ToList();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedTime = now;
                    break;
                case EntityState.Modified:
                    var deletedTimeProp = entry.Property(nameof(IEntity.DeletedTime));

                    if (deletedTimeProp.IsModified && deletedTimeProp.OriginalValue == null && deletedTimeProp.CurrentValue != null)
                    {
                        await HandleCascadeSoftDeleteAsync(context, entry, now, ct);
                    }
                    else if (entry.Entity.DeletedTime == null)
                    {
                        entry.Entity.UpdatedTime = now;
                    }
                    break;
                case EntityState.Deleted:
                    break;
            }
        }
    }

    private async Task HandleCascadeSoftDeleteAsync(DbContext context, EntityEntry entry, DateTimeOffset now, CancellationToken ct)
    {
        var entityType = entry.Metadata.ClrType;

        var cascadeSqls = _recursiveCascadeCache.GetOrAdd(
            entityType,
            _ => BuildRecursiveCascadeSql(entry.Metadata));

        if (!cascadeSqls.Any()) return;

        var pkName = entry.Metadata.FindPrimaryKey()?.Properties[0].Name ?? "Id";
        var parentIdValue = entry.Property(pkName).CurrentValue;

        if (parentIdValue == null) return;

        foreach (var sql in cascadeSqls)
        {
            await context.Database.ExecuteSqlRawAsync(sql, new[] { now, parentIdValue }, ct);
        }
    }

    private static List<string> BuildRecursiveCascadeSql(IEntityType rootType)
    {
        var sqlList = new List<string>();
        var visited = new HashSet<IEntityType>();

        GenerateNestedSql(rootType, "{1}", sqlList, visited);

        return sqlList;
    }

    private static void GenerateNestedSql(IEntityType currentType, string parentIdQuery, List<string> sqlList, HashSet<IEntityType> visited)
    {
        if (!visited.Add(currentType)) return;

        var referencingFks = currentType.GetReferencingForeignKeys();

        foreach (var fk in referencingFks)
        {
            if (fk.DeleteBehavior == DeleteBehavior.Cascade || fk.DeleteBehavior == DeleteBehavior.ClientCascade)
            {
                var dependentType = fk.DeclaringEntityType;

                if (typeof(IEntity).IsAssignableFrom(dependentType.ClrType))
                {
                    var tableName = dependentType.GetTableName();
                    if (tableName == null) continue;
                    var schema = dependentType.GetSchema();
                    var fullTableName = string.IsNullOrEmpty(schema) ? $"\"{tableName}\"" : $"\"{schema}\".\"{tableName}\"";
                    var fkColumnName = fk.Properties[0].GetColumnName(StoreObjectIdentifier.Table(tableName, schema));
                    var pkColumnName = dependentType.FindPrimaryKey()?.Properties[0].GetColumnName(StoreObjectIdentifier.Table(tableName, schema)) ?? "Id";

                    var updateSql = $"UPDATE {fullTableName} SET \"DeletedTime\" = {{0}} WHERE \"{fkColumnName}\" IN ({parentIdQuery}) AND \"DeletedTime\" IS NULL";
                    sqlList.Add(updateSql);

                    var nextParentIdQuery = $"SELECT \"{pkColumnName}\" FROM {fullTableName} WHERE \"{fkColumnName}\" IN ({parentIdQuery})";

                    GenerateNestedSql(dependentType, nextParentIdQuery, sqlList, new HashSet<IEntityType>(visited));
                }
            }
        }
    }

    private bool IsOrphanRemoval(EntityEntry entry)
    {
        var foreignKeys = entry.Metadata.GetForeignKeys();

        foreach (var fk in foreignKeys)
        {
            var parentType = fk.PrincipalEntityType.ClrType;
            var fkProp = fk.Properties[0];
            var parentId = entry.OriginalValues[fkProp];

            if (parentId != null)
            {
                var parentEntry = entry.Context.ChangeTracker.Entries()
                    .Where(e => e.Metadata.ClrType == parentType)
                    .FirstOrDefault(e => Equals(e.Property(fk.PrincipalKey.Properties[0].Name).CurrentValue, parentId));

                if (parentEntry != null && parentEntry.State != EntityState.Deleted)
                {
                    return true;
                }
            }
        }

        return false;
    }

}