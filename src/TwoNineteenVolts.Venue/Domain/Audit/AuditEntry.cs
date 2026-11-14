namespace TwoNineteenVolts.Venue.Domain.Audit;

public class AuditEntry
{
    public long Id { get; private set; }

    public required DateTime OccurredAt { get; init; }

    public required int? ActorUserId { get; init; }

    public required string ActorName { get; init; }

    public required string Action { get; init; }

    public required string? Target { get; init; }

    public required string? Details { get; init; }
}
