using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices.Events;

public record DeviceUpdated(int DeviceId) : IDeviceEvent;
