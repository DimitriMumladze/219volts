using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Infrastructure.Events;

namespace TwoNineteenVolts.Venue.Tests.Events;

public static class PublishedEvents
{
    public static List<IVenueEvent> Take(VenueAppFactory factory)
    {
        var queue = factory.Services.GetRequiredService<EventQueue>();
        var events = new List<IVenueEvent>();
        while (queue.Reader.TryRead(out var venueEvent))
        {
            events.Add(venueEvent);
        }

        return events;
    }
}
