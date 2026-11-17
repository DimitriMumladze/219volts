namespace TwoNineteenVolts.Venue.Application.Devices;

public record OutletState(bool? IsOn, bool DeviceOnline, DateTime? LastSeen, string DeviceName)
{
    public static OutletState Unknown { get; } = new(null, false, null, "");
}
