using System.Threading.Channels;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Infrastructure.Events;

public class EventQueue : IEventQueue
{
    private readonly Channel<IVenueEvent> _channel = Channel.CreateUnbounded<IVenueEvent>();

    public ChannelReader<IVenueEvent> Reader => _channel.Reader;

    public void Publish(IVenueEvent venueEvent)
    {
        _channel.Writer.TryWrite(venueEvent);
    }
}
