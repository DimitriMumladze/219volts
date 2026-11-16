using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record DeviceDeleted(int DeviceId, IReadOnlyList<int> UnmappedSpotIds) : IDeviceEvent;
