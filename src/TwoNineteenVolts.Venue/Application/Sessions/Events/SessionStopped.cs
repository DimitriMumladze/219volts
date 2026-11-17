using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionStopped(int SpotId, int SessionId) : ISpotEvent;
