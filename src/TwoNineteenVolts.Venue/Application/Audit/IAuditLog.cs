namespace TwoNineteenVolts.Venue.Application.Audit;

public interface IAuditLog
{
    // Adds the entry to the current unit of work only; the caller's SaveChangesAsync writes it
    // together with the change it describes.
    void Record(AuditActor actor, string action, string? target = null, string? details = null);
}
