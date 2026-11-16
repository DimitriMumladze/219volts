namespace TwoNineteenVolts.Venue.Domain.Sessions;

public class Session(int spotId, DateTime startedAt, DateTime endsAt, int? startedByUserId, string startedByName)
{
    public int Id { get; private set; }

    public int SpotId { get; private set; } = spotId;

    public DateTime StartedAt { get; private set; } = startedAt;

    public DateTime EndsAt { get; private set; } = endsAt;

    public int? StartedByUserId { get; private set; } = startedByUserId;

    public string StartedByName { get; private set; } = startedByName;

    public DateTime? EndedAt { get; private set; }

    public SessionEndReason? EndReason { get; private set; }

    public int? EndedByUserId { get; private set; }

    public string? EndedByName { get; private set; }

    public bool IsRunning => EndedAt is null;

    public void Extend(int minutes) => EndsAt = EndsAt.AddMinutes(minutes);

    public void End(DateTime at, SessionEndReason reason, int? userId, string name)
    {
        EndedAt = at;
        EndReason = reason;
        EndedByUserId = userId;
        EndedByName = name;
    }
}
