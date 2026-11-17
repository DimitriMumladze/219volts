using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots.Events;

public record SpotUpdated(int SpotId) : ISpotEvent;
