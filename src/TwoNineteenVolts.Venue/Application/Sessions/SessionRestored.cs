using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public record SessionRestored(int SpotId, int SessionId) : ISpotEvent;
