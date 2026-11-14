using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditActor(int? UserId, string Name, Role? Role)
{
    public static AuditActor Unknown { get; } = new(null, "unknown", null);
}
