namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditActor(int? UserId, string Name)
{
    public static AuditActor Unknown { get; } = new(null, "unknown");
}
