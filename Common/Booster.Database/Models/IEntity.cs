namespace Booster.Database.Models;

public interface IEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public DateTimeOffset? UpdatedTime { get; set; }
    public DateTimeOffset? DeletedTime { get; set; }
}

public interface IIsolatedEntity : IEntity
{
    public Guid IsolationId { get; set; }
}

public interface IFileAttachableEntity : IEntity
{
    public List<FileAttachment>? Attachments { get; set; }
}

public class FileAttachment 
{
    public Guid FileId { get; set; }
    public int Order { get; set; }
}

public interface ITranslationParent<T>
    where T : IEntity
{
    public ICollection<T> Translations { get; set; }
}

public interface ITranslation
{
    public string LanguageCode { get; set; }
}
