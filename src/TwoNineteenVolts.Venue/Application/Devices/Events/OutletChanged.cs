using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices.Events;

public record OutletChanged(Outlet Outlet, bool? IsOn) : IDeviceEvent
{
    public int DeviceId => Outlet.DeviceId;
}
