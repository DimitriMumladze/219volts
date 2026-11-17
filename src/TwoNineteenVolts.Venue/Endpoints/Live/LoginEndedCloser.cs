using TwoNineteenVolts.Venue.Application.Auth.Events;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Endpoints.Live;

public class LoginEndedCloser(HubConnections connections) : IEventListener
{
    public Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
    {
        if (venueEvent is LoginEnded ended)
        {
            connections.CloseAllFor(ended.UserId);
        }

        return Task.CompletedTask;
    }
}
