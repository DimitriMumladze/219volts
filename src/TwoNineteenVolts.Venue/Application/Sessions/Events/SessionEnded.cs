using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionEnded(int SpotId, int SessionId) : ISpotEvent;
