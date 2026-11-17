namespace TwoNineteenVolts.Venue.Application.Devices;

public class DeviceLocks
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, SemaphoreSlim> _locks = [];

    public SemaphoreSlim For(int deviceId)
    {
        lock (_lock)
        {
            if (!_locks.TryGetValue(deviceId, out var deviceLock))
            {
                deviceLock = new SemaphoreSlim(1, 1);
                _locks[deviceId] = deviceLock;
            }

            return deviceLock;
        }
    }

    public void Remove(int deviceId)
    {
        lock (_lock)
        {
            _locks.Remove(deviceId);
        }
    }
}
