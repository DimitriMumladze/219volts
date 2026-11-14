namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditActor(int? UserId, string Name)
{
    // Used for failed logins, where nobody is signed in yet.
    public static AuditActor Unknown { get; } = new(null, "unknown");
}
