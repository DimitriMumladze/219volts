using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotSwitched(int SpotId, bool IsOn) : ISpotEvent;
