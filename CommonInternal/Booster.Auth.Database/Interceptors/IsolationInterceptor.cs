using Booster.Auth.Database.Constants;
using Booster.Auth.Manager.RequestContexts;
using Booster.Database.Models;
using Booster.ExceptionManager.Abstraction.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Booster.Auth.Database.Interceptors;

public sealed class IsolationInterceptor(IRequestContextAccessor requestContextAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyIsolation(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyIsolation(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyIsolation(DbContext? context)
    {
        if (context is null)
            return;

        var isolation = requestContextAccessor.Context().Isolation;

        foreach (var entry in context.ChangeTracker.Entries<IIsolatedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (isolation == Guid.Empty)
                        throw new BusinessException(IsolationMessageKeys.InvalidIsolation);

                    entry.Entity.IsolationId = isolation;
                    break;

                case EntityState.Modified:
                    if (entry.Property(nameof(IIsolatedEntity.IsolationId)).IsModified)
                        throw new BusinessException(IsolationMessageKeys.IsolationFieldImmutable);
                    break;
            }
        }
    }
}
