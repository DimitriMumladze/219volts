using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Tests.Events;

public class RecordingListener : IEventListener
{
    private readonly Lock _lock = new();
    private readonly List<IVenueEvent> _events = [];

    public TaskCompletionSource? Gate { get; set; }

    public bool Throws { get; set; }

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

    public async Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
    {
        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (Throws)
        {
            throw new InvalidOperationException("listener failed");
        }

        lock (_lock)
        {
            _events.Add(venueEvent);
        }
    }
}
