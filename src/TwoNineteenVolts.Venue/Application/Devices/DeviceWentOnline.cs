using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record DeviceWentOnline(int DeviceId) : IDeviceEvent;
