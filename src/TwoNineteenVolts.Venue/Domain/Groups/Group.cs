namespace TwoNineteenVolts.Venue.Domain.Groups;

public class Group(string name, long hourlyRateTetri, DateTime createdAt)
{
    public int Id { get; private set; }

    public string Name { get; private set; } = name;

    public string NameKey { get; private set; } = name.ToUpperInvariant();

    public long HourlyRateTetri { get; private set; } = hourlyRateTetri;

    public bool IsArchived { get; private set; }

    public DateTime CreatedAt { get; private set; } = createdAt;

    public void Rename(string name)
    {
        Name = name;
        NameKey = name.ToUpperInvariant();
    }

    public void SetRate(long hourlyRateTetri) => HourlyRateTetri = hourlyRateTetri;

    public void Archive() => IsArchived = true;
}
