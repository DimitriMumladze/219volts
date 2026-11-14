namespace TwoNineteenVolts.Venue.Domain.Devices;

public class Device(string name, DeviceKind kind, string ipAddress, int port, int channelCount, DateTime createdAt)
{
    public int Id { get; private set; }

    public string Name { get; private set; } = name;

    public string NameKey { get; private set; } = name.ToUpperInvariant();

    public DeviceKind Kind { get; private set; } = kind;

    public string IpAddress { get; private set; } = ipAddress;

    public int Port { get; private set; } = port;

    public int ChannelCount { get; private set; } = channelCount;

    public DateTime CreatedAt { get; private set; } = createdAt;

    public void Update(string name, string ipAddress, int port)
    {
        Name = name;
        NameKey = name.ToUpperInvariant();
        IpAddress = ipAddress;
        Port = port;
    }
}
