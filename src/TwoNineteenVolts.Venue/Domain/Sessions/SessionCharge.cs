namespace TwoNineteenVolts.Venue.Domain.Sessions;

public class SessionCharge
{
    public long Id { get; private set; }

    public required int SessionId { get; init; }

    public required ChargeKind Kind { get; init; }

    public required DateTime At { get; init; }

    public required int? UserId { get; init; }

    public required string UserName { get; init; }

    public required int Minutes { get; init; }

    public required long ComputedTetri { get; init; }

    public required long ChargedTetri { get; init; }
}
