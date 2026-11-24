using Booster.Auth.Common.Internal.Amqp.Events;
using Booster.Auth.Idp.Features._base;
using Booster.Auth.Manager.RequestContexts;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Booster.Auth.Idp.Features._amqp.Consumers.PoolDefinitionChanged;

/// <summary>Management.Api'de anahtarı değişen ya da silinen rolü kullanıcı atamalarında uygular.</summary>
public class RoleChangedEventConsumer(IdpDbContext db, IRequestContextAccessor requestContextAccessor)
    : IConsumer<RoleChangedEvent>
{
    public async Task Consume(ConsumeContext<RoleChangedEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        using var _ = requestContextAccessor.BeginSystemContext(message.UserPoolId);

        var rows = db.PrincipalRoles.Where(r => r.RoleId == message.RoleId);

        if (string.IsNullOrEmpty(message.NewKey))
        {
            await rows.ExecuteDeleteAsync(ct);
            return;
        }

        await rows.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.RoleName, message.NewKey)
            .SetProperty(x => x.UpdatedTime, (DateTimeOffset?)DateTimeOffset.UtcNow), ct);
    }
}
