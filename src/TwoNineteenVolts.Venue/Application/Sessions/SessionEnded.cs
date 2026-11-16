using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public record SessionEnded(int SpotId, int SessionId) : ISpotEvent;
