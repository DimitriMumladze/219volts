using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotArchived(int SpotId) : ISpotEvent;
