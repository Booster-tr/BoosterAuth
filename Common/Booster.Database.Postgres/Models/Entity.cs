using Booster.Database.Models;

namespace Booster.Database.Postgres.Models;

public class Entity : IEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset? UpdatedTime { get; set; }
    public DateTimeOffset? DeletedTime { get; set; }
}

public class IsolatedEntity : Entity, IIsolatedEntity
{
    public Guid IsolationId { get; set; }
}


public class FileAttachableEntity : Entity, IFileAttachableEntity
{
    public List<FileAttachment>? Attachments { get; set; } = new List<FileAttachment>();
}