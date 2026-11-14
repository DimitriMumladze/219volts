namespace TwoNineteenVolts.Venue.Endpoints.Audit;

public record AuditEntryResponse(
    long Id,
    DateTime OccurredAt,
    int? ActorUserId,
    string ActorName,
    string Action,
    string? Target,
    string? Details);
