namespace TwoNineteenVolts.Venue.Application.Devices;

// Channels: the states from the last successful read, index 0 = channel 1; empty = never read.
public record DeviceStatus(bool IsOnline, DateTime? LastSeen, IReadOnlyList<bool>? Channels)
{
    public static DeviceStatus Unknown { get; } = new(false, null, null);

    // Unknown while the device is offline: the last read may no longer be true.
    public bool? IsOn(int channel)
    {
        if (!IsOnline || Channels is null || channel < 1 || channel > Channels.Count)
        {
            return null;
        }

        return Channels[channel - 1];
    }
}
