using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Manager.RequestContexts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features._amqp.Consumers.PoolDefinitionChanged;

/// <summary>Management.Api'de adı değişen ya da silinen kapsamı kullanıcılara verilmiş kopyalarda uygular.</summary>
public class ScopeDefinitionChangedEventConsumer(IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : IConsumer<ScopeDefinitionChangedEvent>
{
    public async Task Consume(ConsumeContext<ScopeDefinitionChangedEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        using var _ = requestContextAccessor.BeginSystemContext(message.UserPoolId);

        var oldRows = db.PrincipalScopes.Where(s => s.Scope == message.OldScope);

        if (string.IsNullOrEmpty(message.NewScope))
        {
            await oldRows.ExecuteDeleteAsync(ct);
            return;
        }

        var newScope = message.NewScope;

        // Yeni adı zaten taşıyan kullanıcıda eski satır silinir; yoksa (PrincipalId, Scope) benzersiz indeksi bozulurdu.
        await oldRows
            .Where(s => db.PrincipalScopes.Any(x => x.PrincipalId == s.PrincipalId && x.Scope == newScope))
            .ExecuteDeleteAsync(ct);

        await oldRows.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Scope, newScope)
            .SetProperty(x => x.UpdatedTime, (DateTimeOffset?)DateTimeOffset.UtcNow), ct);
    }
}
