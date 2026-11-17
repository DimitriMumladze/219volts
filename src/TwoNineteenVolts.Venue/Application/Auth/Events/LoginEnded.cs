using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Auth.Events;

public record LoginEnded(int UserId) : IVenueEvent;
