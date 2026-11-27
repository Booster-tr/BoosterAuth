using Booster.Auth.Common.Internal.Notifications;
using Booster.Auth.Idp.Features._base.Entities;
using Booster.Auth.Idp.Options;
using Booster.Auth.Manager.RequestContexts;
using Booster.Caching.Abstraction;

namespace Booster.Auth.Idp.Managers.UserManagers;

/// <summary>Davet baglantisinin tasidigi kayit. Token'in kendisi saklanmaz: anahtar Redis'te SHA-256 ile tutulur.</summary>
public sealed class InvitationData
{
    public Guid PrincipalId { get; init; }
    public Guid IsolationId { get; init; }
    public string Email { get; init; } = default!;
    /// <summary>Kullanicinin GUNCEL davetinin numarasi; yeniden gonderilen davet eskisini gecersiz kilar.</summary>
    public Guid Nonce { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Kullanicinin guncel davet numarasi (token degil).</summary>
public sealed class InvitationPendingData
{
    public Guid Nonce { get; init; }
}

/// <summary>
/// Davet: rastgele token + e-posta. Token 1 gun gecerli ve tek kullanimliktir (bildirim sablonu da "1 gun" der).
/// <para>
/// E-posta HEMEN ve yanit beklenmeden gider (<see cref="NotificationClient"/>). Bu yuzden cagiran, kullanici kaydini
/// commit ettikten SONRA <see cref="SendAsync"/> cagirir; geri alinan bir kayit icin davet gitmez.
/// </para>
/// </summary>
public sealed class InvitationManager(
    ISecureCacheStore<InvitationData> invitationStore,
    ISecureCacheStore<InvitationPendingData> pendingStore,
    NotificationClient notifications,
    IdpUiOptions uiOptions,
    IRequestContextAccessor requestContextAccessor)
{
    public static readonly TimeSpan Ttl = TimeSpan.FromDays(1);

    public static string PendingKey(Guid isolationId, Guid principalId) => $"invite-pending:{isolationId}:{principalId}";

    public async Task SendAsync(Principal principal, Guid isolationId, CancellationToken ct)
    {
        var nonce = Guid.NewGuid();
        var data = new InvitationData
        {
            PrincipalId = principal.Id,
            IsolationId = isolationId,
            Email = principal.Email!,
            Nonce = nonce,
            ExpiresAt = DateTimeOffset.UtcNow.Add(Ttl)
        };

        var token = await invitationStore.StoreAsync(data, Ttl, ct);
        await pendingStore.StoreAsync(PendingKey(isolationId, principal.Id), new InvitationPendingData { Nonce = nonce }, Ttl, ct);

        notifications.Send(new NotificationRequest
        {
            TenantId = Guid.Empty,
            UserPoolId = isolationId,
            PrincipalId = principal.Id,
            TemplateKey = NotificationTemplateKeys.UserInvitation,
            Channel = NotificationChannel.Email,
            Target = principal.Email!,
            LanguageCode = requestContextAccessor.Context().AcceptLanguage,
            Data = new Dictionary<string, string>
            {
                ["DisplayName"] = principal.DisplayName,
                ["InvitationUrl"] = InvitationUrl(token)
            }
        });
    }

    /// <summary>Gecerli, suresi dolmamis ve GUNCEL davet ise kaydi doner; degilse null.</summary>
    public async Task<InvitationData?> ResolveAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var data = await invitationStore.GetAsync(token, ct);
        if (data is null || data.ExpiresAt <= DateTimeOffset.UtcNow) return null;

        var pending = await pendingStore.GetAsync(PendingKey(data.IsolationId, data.PrincipalId), ct);
        return pending?.Nonce == data.Nonce ? data : null;
    }

    /// <summary>Daveti kullanilmis sayar (tek kullanimlik).</summary>
    public async Task ConsumeAsync(string token, InvitationData data, CancellationToken ct)
    {
        await invitationStore.RemoveAsync(token, ct);
        await pendingStore.RemoveAsync(PendingKey(data.IsolationId, data.PrincipalId), ct);
    }

    private string InvitationUrl(string token) =>
        $"{uiOptions.BaseUrl.TrimEnd('/')}/ui/invite?token={Uri.EscapeDataString(token)}";
}
