using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionStarted(int SpotId, int SessionId) : ISpotEvent;
