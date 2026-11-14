using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Audit;

public class AuditLog(VenueDbContext db, TimeProvider time) : IAuditLog
{
    public void Record(AuditActor actor, string action, string? target = null, string? details = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            OccurredAt = time.GetUtcNow().UtcDateTime,
            ActorUserId = actor.UserId,
            ActorName = actor.Name,
            Action = action,
            Target = target,
            Details = details
        });
    }
}
