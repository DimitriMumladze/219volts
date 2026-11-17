using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices.Events;

public record DeviceWentOffline(int DeviceId) : IDeviceEvent;
