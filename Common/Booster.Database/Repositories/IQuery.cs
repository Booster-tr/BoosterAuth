namespace Booster.Database.Repositories;

public interface IQuery<T>
{
    IQueryable<T> Query();
}
