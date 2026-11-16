using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record DeviceRegistered(int DeviceId) : IDeviceEvent;
