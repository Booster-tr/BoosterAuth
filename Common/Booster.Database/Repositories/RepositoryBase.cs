using Booster.Database.DynamicFilter;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Models;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Booster.Database.Repositories;

public class RepositoryBase<TEntity, TContext> : IAsyncRepository<TEntity, TContext>
    where TEntity : class, IEntity, new()
    where TContext : DbContext
{
    protected readonly TContext Context;

    public RepositoryBase(TContext context)
    {
        Context = context;
    }

    protected virtual IQueryable<TEntity> ApplywithoutGlobalFilter(IQueryable<TEntity> query)
    {
        query = query.IgnoreQueryFilters();
        return query;
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Context.AddAsync(entity, cancellationToken);
        return entity;
    }

    public virtual async Task<ICollection<TEntity>> AddRangeAsync(ICollection<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await Context.AddRangeAsync(entities, cancellationToken);
        return entities;
    }

    public virtual TEntity Update(TEntity entity)
    {
        Context.Update(entity);
        return entity;
    }

    public virtual ICollection<TEntity> UpdateRange(ICollection<TEntity> entities)
    {
        Context.UpdateRange(entities);
        return entities;
    }

    public virtual TEntity Delete(TEntity entity, bool softDelete = true)
    {
        if (softDelete)
        {
            entity.DeletedTime = DateTime.UtcNow;
            Update(entity);
            return entity;
        }
        Context.Remove(entity);
        return entity;

    }

    public virtual ICollection<TEntity> DeleteRange(ICollection<TEntity> entities)
    {
        Context.RemoveRange(entities);
        return entities;
    }

    public virtual IQueryable<TEntity> Query() => Context.Set<TEntity>();

    public virtual async Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking) queryable = queryable.AsNoTracking();
        if (include != null) queryable = include(queryable);

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        return await queryable.TagWith(nameof(TEntity)).FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<Paginate<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking) queryable = queryable.AsNoTracking();
        if (include != null) queryable = include(queryable);

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        if (predicate != null) queryable = queryable.Where(predicate);
        if (orderBy != null)
            return await orderBy(queryable).TagWith(nameof(TEntity)).ToPaginateAsync(index, size, cancellationToken);

        return await queryable.TagWith(nameof(TEntity)).ToPaginateAsync(index, size, cancellationToken);
    }

    public virtual async Task<Paginate<TEntity>> ListDynamicAsync(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();
        if (!enableTracking) queryable = queryable.AsNoTracking();
        if (include != null) queryable = include(queryable);

        queryable = queryable.ToDynamic(dynamic);

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        if (predicate != null) queryable = queryable.Where(predicate);

        return await queryable.TagWith(nameof(TEntity)).ToPaginateAsync(index, size, cancellationToken);
    }

    public virtual async Task<TResult?> GetProjectedAsync<TResult>(
    Expression<Func<TEntity, bool>> predicate,
    Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
    bool withoutGlobalFilter = false,
    CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query().AsNoTracking();
        if (include != null) queryable = include(queryable);
        if (withoutGlobalFilter) queryable = ApplywithoutGlobalFilter(queryable);

        return await queryable
            .TagWith($"{nameof(TEntity)}_Projected")
            .Where(predicate)
            .ProjectToType<TResult>()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<Paginate<TResult>> ListProjectedAsync<TResult>(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query().AsNoTracking();
        if (include != null) queryable = include(queryable);
        if (withoutGlobalFilter) queryable = ApplywithoutGlobalFilter(queryable);
        if (predicate != null) queryable = queryable.Where(predicate);

        if (orderBy != null) queryable = orderBy(queryable);

        return await queryable
            .TagWith($"{nameof(TEntity)}_ListProjected")
            .ProjectToType<TResult>()
            .ToPaginateAsync(index, size, cancellationToken);
    }

    public virtual async Task<Paginate<TResult>> ListDynamicProjectedAsync<TResult>(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query().AsNoTracking();
        if (include != null) queryable = include(queryable);

        if (dynamic != null) queryable = queryable.ToDynamic(dynamic);

        if (withoutGlobalFilter) queryable = ApplywithoutGlobalFilter(queryable);
        if (predicate != null) queryable = queryable.Where(predicate);

        return await queryable
            .TagWith($"{nameof(TEntity)}_ListDynamicProjected")
            .ProjectToType<TResult>()
            .ToPaginateAsync(index, size, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        if (predicate != null) queryable = queryable.Where(predicate);

        return await queryable.TagWith(nameof(TEntity)).AnyAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        if (predicate != null) queryable = queryable.Where(predicate);

        return await queryable.TagWith(nameof(TEntity)).CountAsync(cancellationToken);
    }

    public virtual async Task<int> CountDynamicAsync(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> queryable = Query();

        if (dynamic != null) queryable = queryable.ToDynamic(dynamic);

        if (withoutGlobalFilter)
            queryable = ApplywithoutGlobalFilter(queryable);

        if (predicate != null) queryable = queryable.Where(predicate);

        return await queryable.TagWith(nameof(TEntity)).CountAsync(cancellationToken);
    }

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync();
    }

    public virtual void SaveChanges(CancellationToken cancellationToken = default)
    {
        Context.SaveChanges();
    }
}
