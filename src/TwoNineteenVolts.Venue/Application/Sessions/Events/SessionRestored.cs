using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionRestored(int SpotId, int SessionId) : ISpotEvent;
