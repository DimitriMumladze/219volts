using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices.Events;

public record DeviceWentOnline(int DeviceId) : IDeviceEvent;
