using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotCreated(int SpotId) : ISpotEvent;
