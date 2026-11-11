using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditActor(int? UserId, string Name, AuditSource Source)
{
    // Used for failed logins, where nobody is signed in yet.
    public static AuditActor Unknown { get; } = new(null, "unknown", AuditSource.Local);
}
