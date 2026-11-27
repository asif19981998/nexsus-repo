using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Nexus.Application.Common;
using Nexus.Application.Security;
using Nexus.Domain.Abstractions;

namespace Nexus.Infrastructure.Persistence;

/// <summary>Fills CreatedAt/CreatedById and LastModifiedAt/LastModifiedById on every <see cref="IEntity"/> being saved.</summary>
public sealed class AuditableEntityInterceptor(ICurrentUserService me, IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        var now = clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<IEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.CreatedById ??= me.UserId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedAt = now;
                entry.Entity.LastModifiedById = me.UserId;
            }
        }
    }
}
