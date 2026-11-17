namespace TwoNineteenVolts.Venue.Application.Sessions;

public class SwitchOffWarnings
{
    private readonly Lock _lock = new();
    private readonly HashSet<int> _spotIds = [];

    public void Add(int spotId)
    {
        lock (_lock)
        {
            _spotIds.Add(spotId);
        }
    }

    public void Clear(int spotId)
    {
        lock (_lock)
        {
            _spotIds.Remove(spotId);
        }
    }

    public bool HasWarning(int spotId)
    {
        lock (_lock)
        {
            return _spotIds.Contains(spotId);
        }
    }
}
