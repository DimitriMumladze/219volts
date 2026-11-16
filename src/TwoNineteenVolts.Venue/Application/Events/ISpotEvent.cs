namespace TwoNineteenVolts.Venue.Application.Events;

public interface ISpotEvent : IVenueEvent
{
    int SpotId { get; }
}
