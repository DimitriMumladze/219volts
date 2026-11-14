using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Endpoints.Devices;

public record DeviceResponse(
    int Id,
    string Name,
    DeviceKind Kind,
    string IpAddress,
    int Port,
    int ChannelCount,
    bool IsOnline,
    DateTime? LastSeen,
    DateTime CreatedAt);
