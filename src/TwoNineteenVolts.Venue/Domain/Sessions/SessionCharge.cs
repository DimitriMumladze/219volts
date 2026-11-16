namespace TwoNineteenVolts.Venue.Domain.Sessions;

public class SessionCharge(
    int sessionId,
    ChargeKind kind,
    DateTime at,
    int? userId,
    string userName,
    int minutes,
    long computedTetri,
    long chargedTetri)
{
    public int Id { get; private set; }

    public int SessionId { get; private set; } = sessionId;

    public ChargeKind Kind { get; private set; } = kind;

    public DateTime At { get; private set; } = at;

    public int? UserId { get; private set; } = userId;

    public string UserName { get; private set; } = userName;

    public int Minutes { get; private set; } = minutes;

    public long ComputedTetri { get; private set; } = computedTetri;

    public long ChargedTetri { get; private set; } = chargedTetri;
}
