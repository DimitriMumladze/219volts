using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public record SessionStarted(int SpotId, int SessionId) : ISpotEvent;
