using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Audit.Contracts;

public record AuditActor(int? UserId, string Name, Role? Role)
{
    public static AuditActor Unknown { get; } = new(null, "unknown", null);

    public static AuditActor Timer { get; } = new(null, "Timer", null);

    public static AuditActor DriftFix { get; } = new(null, "Drift fix", null);

    public static AuditActor Restart { get; } = new(null, "Restart", null);
}
