namespace TwoNineteenVolts.Venue.Application.Devices.Contracts;

public record DeviceStatus(bool IsOnline, DateTime? LastSeen, IReadOnlyList<bool?>? Channels)
{
    public static DeviceStatus Unknown { get; } = new(false, null, null);

    public bool? IsOn(int channel)
    {
        if (!IsOnline || Channels is null || channel < 1 || channel > Channels.Count)
        {
            return null;
        }

        return Channels[channel - 1];
    }
}
