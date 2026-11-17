using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record OutletChanged(Outlet Outlet, bool? IsOn) : IDeviceEvent
{
    public int DeviceId => Outlet.DeviceId;
}
