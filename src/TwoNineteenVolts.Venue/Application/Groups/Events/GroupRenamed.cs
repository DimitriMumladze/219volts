using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups.Events;

public record GroupRenamed(int GroupId) : IGroupEvent;
