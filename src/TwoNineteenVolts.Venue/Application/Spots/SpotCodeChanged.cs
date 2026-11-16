using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotCodeChanged(int SpotId) : ISpotEvent;
