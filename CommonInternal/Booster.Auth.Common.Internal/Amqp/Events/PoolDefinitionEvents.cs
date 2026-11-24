namespace Booster.Auth.Common.Internal.Amqp.Events;

/// <summary>Havuzda bir kapsam tanımının adı değişti ya da silindi. <see cref="NewScope"/> boşsa silinmiştir.</summary>
public record ScopeDefinitionChangedEvent
{
    public Guid UserPoolId { get; init; }
    public string OldScope { get; init; } = default!;
    public string? NewScope { get; init; }
}

/// <summary>Havuzda bir rolün anahtarı değişti ya da rol silindi. <see cref="NewKey"/> boşsa silinmiştir.</summary>
public record RoleChangedEvent
{
    public Guid UserPoolId { get; init; }
    public Guid RoleId { get; init; }
    public string? NewKey { get; init; }
}

/// <summary>Management.Api üretir, IdP kullanıcılara kopyalanmış adları günceller.</summary>
public static class PoolDefinitionTopics
{
    public const string ScopeDefinitionChanged = "auth-scope-definition-changed-topic";
    public const string RoleChanged = "auth-role-changed-topic";
}
