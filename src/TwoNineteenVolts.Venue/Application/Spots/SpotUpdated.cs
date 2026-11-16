using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotUpdated(int SpotId) : ISpotEvent;
