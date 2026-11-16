using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public record SessionStopped(int SpotId, int SessionId) : ISpotEvent;
