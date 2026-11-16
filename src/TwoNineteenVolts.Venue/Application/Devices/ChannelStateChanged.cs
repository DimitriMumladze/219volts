using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public record ChannelStateChanged(int DeviceId, int Channel, bool? IsOn) : IDeviceEvent;
