namespace TwoNineteenVolts.Venue.Application.Events;

public interface IDeviceEvent : IVenueEvent
{
    int DeviceId { get; }
}
