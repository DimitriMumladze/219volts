namespace TwoNineteenVolts.Venue.Application.Events;

public interface IEventQueue
{
    void Publish(IVenueEvent venueEvent);
}
