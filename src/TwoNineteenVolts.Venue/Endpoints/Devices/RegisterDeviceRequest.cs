using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Endpoints.Devices;

public record RegisterDeviceRequest(string Name, DeviceKind Kind, string IpAddress, int ChannelCount, int? Port = null);
