using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Tests.Events;

public record TestEvent(string Name) : IVenueEvent;
