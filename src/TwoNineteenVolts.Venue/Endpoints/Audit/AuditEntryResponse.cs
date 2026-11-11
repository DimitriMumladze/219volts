using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Endpoints.Audit;

public record AuditEntryResponse(
    long Id,
    DateTime OccurredAt,
    int? ActorUserId,
    string ActorName,
    AuditSource Source,
    string Action,
    string? Target,
    string? Details);
