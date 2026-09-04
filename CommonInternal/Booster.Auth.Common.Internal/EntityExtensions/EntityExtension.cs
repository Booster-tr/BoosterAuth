using Booster.Auth.Database.Models;
using Booster.Database.DynamicFilter.Models;
using Booster.Database.Models;
using Booster.ExceptionManager.Abstraction.Constants;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Mapster;

namespace Booster.Auth.Common.Internal.EntityExtensions;


public static class EntityExtensions
{
    public static void SetId(this IEntity entity)
    {
        if (entity.Id != Guid.Empty)
            return;
        entity.Id = EntityId.New();
    }


    public static void ThrowExceptionIfDataNull(this object? entity, Exception? ex = null)
    {
        if (entity == null)
        {
            if (ex == null)
                throw new BusinessException(ValidationErrors.DataIsNullException);

            throw ex;
        }
    }

    public static void ThrowCustomExceptionIfDataNull(this object? entity, string messageKey, Exception? ex = null)
    {
        if (entity == null)
        {
            if (ex == null)
                throw new BusinessException(messageKey);

            throw ex;
        }
    }

    public static ListModel<T2> MapListModel<T, T2>(this Paginate<T> data, BaseDynamicRequest dynamicRequest)
    {
        return new ListModel<T2>
        {
            Count = data.Count,
            PageRequest = dynamicRequest.PageRequest,
            DynamicQuery = dynamicRequest.DynamicQuery ?? new DynamicQuery(),
            HasNext = data.HasNext,
            Size = data.Size,
            Index = data.Index,
            HasPrevious = data.HasPrevious,
            Items = data.Items.Adapt<List<T2>>(),
            Pages = data.Pages
        };
    }
}
public static class EntityId
{
    public static Guid New() => Guid.CreateVersion7();
}
