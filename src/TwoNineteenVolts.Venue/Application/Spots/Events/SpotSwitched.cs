using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots.Events;

public record SpotSwitched(int SpotId, bool IsOn) : ISpotEvent;
