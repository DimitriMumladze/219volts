using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record DeviceWentOffline(int DeviceId) : IDeviceEvent;
