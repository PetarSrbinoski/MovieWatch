using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MovieWatch.Domain.Common;

namespace MovieWatch.Web.Interceptor;

public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor accessor, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
            return;
        var actor = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var now = clock.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.Stamp(now, actor, entry.State == EntityState.Added);
        }
    }
}
