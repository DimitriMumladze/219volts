using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions.Events;

public record SessionExtended(int SpotId, int SessionId) : ISpotEvent;
