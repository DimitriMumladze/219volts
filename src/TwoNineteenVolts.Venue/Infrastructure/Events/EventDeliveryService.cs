using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Infrastructure.Events;

public class EventDeliveryService(
    EventQueue queue,
    IServiceScopeFactory scopes,
    ILogger<EventDeliveryService> logger) : BackgroundService
{
    public async Task RunOnceAsync()
    {
        while (queue.Reader.TryRead(out var venueEvent))
        {
            await DeliverAsync(venueEvent, CancellationToken.None);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var events = queue.Reader.ReadAllAsync(stoppingToken);
        await foreach (var venueEvent in events)
        {
            await DeliverAsync(venueEvent, stoppingToken);
        }
    }


    private async Task DeliverAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var listeners = ListenersFor(scope, venueEvent);
        foreach (var listener in listeners)
        {
            try
            {
                await listener.HandleAsync(venueEvent, cancellationToken);
            }
            catch (Exception exception)
            {
                var listenerName = listener.GetType().Name;
                var eventName = venueEvent.GetType().Name;
                logger.LogError(exception, "Event listener {Listener} failed on {Event}", listenerName, eventName);
            }
        }
    }

    private IReadOnlyList<IEventListener> ListenersFor(IServiceScope scope, IVenueEvent venueEvent)
    {
        try
        {
            return scope.ServiceProvider.GetServices<IEventListener>().ToList();
        }
        catch (Exception exception)
        {
            var eventName = venueEvent.GetType().Name;
            logger.LogError(exception, "Event listeners could not be created for {Event}", eventName);
            return [];
        }
    }
}
