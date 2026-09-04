using Booster.Database.Models;

namespace Booster.Auth.Database.Constants;

public class BasePropertyNames
{
    public static readonly string IsolationIdPropName = nameof(IIsolatedEntity.IsolationId);
    public static readonly string DeletedTimePropName = nameof(IEntity.DeletedTime);
}
