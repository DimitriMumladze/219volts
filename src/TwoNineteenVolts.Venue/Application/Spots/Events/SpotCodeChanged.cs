using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots.Events;

public record SpotCodeChanged(int SpotId) : ISpotEvent;
