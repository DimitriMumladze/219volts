using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Endpoints.Devices;

// Port may be left out: the kind's default port is used.
public record RegisterDeviceRequest(string Name, DeviceKind Kind, string IpAddress, int ChannelCount, int? Port = null);
