using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices.Events;

public record DeviceRegistered(int DeviceId) : IDeviceEvent;
