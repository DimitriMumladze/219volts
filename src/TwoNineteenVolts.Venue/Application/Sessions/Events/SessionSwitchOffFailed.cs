using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionSwitchOffFailed(int SpotId, int SessionId) : ISpotEvent;
