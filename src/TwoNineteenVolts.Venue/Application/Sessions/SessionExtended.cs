using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public record SessionExtended(int SpotId, int SessionId) : ISpotEvent;
