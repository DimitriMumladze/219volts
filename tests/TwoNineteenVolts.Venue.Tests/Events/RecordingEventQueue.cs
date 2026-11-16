using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Tests.Events;

public class RecordingEventQueue : IEventQueue
{
    private readonly Lock _lock = new();
    private readonly List<IVenueEvent> _events = [];

    public IReadOnlyList<IVenueEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public void Publish(IVenueEvent venueEvent)
    {
        lock (_lock)
        {
            _events.Add(venueEvent);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
        }
    }
}
