namespace TwoNineteenVolts.Api.Features.Devices.Registry;

public class DeviceRegistry
{
    private readonly HashSet<string> _deviceIds = [];
    private readonly Lock _lock = new();

    public void Add(string deviceId)
    {
        lock (_lock)
        {
            _deviceIds.Add(deviceId);
        }
    }

    public bool Contains(string deviceId)
    {
        lock (_lock)
        {
            return _deviceIds.Contains(deviceId);
        }
    }
}
