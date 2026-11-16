namespace TwoNineteenVolts.Venue.Application.Events;

public interface IEventListener
{
    Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken);
}
