using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record DeviceUpdated(int DeviceId) : IDeviceEvent;
