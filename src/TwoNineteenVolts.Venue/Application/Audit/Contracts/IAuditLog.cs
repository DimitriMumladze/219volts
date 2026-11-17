namespace TwoNineteenVolts.Venue.Application.Audit.Contracts;

public interface IAuditLog
{
    void Record(AuditActor actor, string action, string? target = null, string? details = null);
}
