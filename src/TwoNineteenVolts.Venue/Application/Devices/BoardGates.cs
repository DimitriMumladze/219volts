namespace TwoNineteenVolts.Venue.Application.Devices;

public class BoardGates
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, SemaphoreSlim> _gates = [];

    public SemaphoreSlim For(int deviceId)
    {
        lock (_lock)
        {
            if (!_gates.TryGetValue(deviceId, out var gate))
            {
                gate = new SemaphoreSlim(1, 1);
                _gates[deviceId] = gate;
            }

            return gate;
        }
    }

    public void Remove(int deviceId)
    {
        lock (_lock)
        {
            _gates.Remove(deviceId);
        }
    }
}
