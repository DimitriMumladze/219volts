namespace TwoNineteenVolts.Venue.Application.Devices.Contracts;

public record OutletState(bool? IsOn, bool IsDeviceOnline, DateTime? LastSeen, string DeviceName)
{
    public static OutletState Unknown { get; } = new(null, false, null, "");
}
