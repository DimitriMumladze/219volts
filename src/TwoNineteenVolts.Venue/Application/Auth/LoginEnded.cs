using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Auth;

public record LoginEnded(int UserId) : IVenueEvent;
