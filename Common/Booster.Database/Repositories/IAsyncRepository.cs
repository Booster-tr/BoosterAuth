using Booster.Database.DynamicFilter.Models;
using Booster.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace Booster.Database.Repositories;

public interface IAsyncRepository<TEntity, TContext>
    where TEntity : class, IEntity, new()
    where TContext : DbContext
{
    IQueryable<TEntity> Query();

    Task<TEntity?> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default);

    Task<Paginate<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default);

    Task<Paginate<TEntity>> ListDynamicAsync(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        bool enableTracking = true,
        CancellationToken cancellationToken = default);

    Task<TResult?> GetProjectedAsync<TResult>(
        Expression<Func<TEntity, bool>> predicate,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);

    Task<Paginate<TResult>> ListProjectedAsync<TResult>(
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);

    Task<Paginate<TResult>> ListDynamicProjectedAsync<TResult>(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        int index = 0,
        int size = 10,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);

    Task<int> CountDynamicAsync(
        DynamicQuery? dynamic,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool withoutGlobalFilter = false,
        CancellationToken cancellationToken = default);



    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<ICollection<TEntity>> AddRangeAsync(ICollection<TEntity> entities, CancellationToken cancellationToken = default);

    TEntity Update(TEntity entity);

    ICollection<TEntity> UpdateRange(ICollection<TEntity> entities);

    TEntity Delete(TEntity entity, bool softDelete = true);

    ICollection<TEntity> DeleteRange(ICollection<TEntity> entities);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void SaveChanges(CancellationToken cancellationToken = default);
}
