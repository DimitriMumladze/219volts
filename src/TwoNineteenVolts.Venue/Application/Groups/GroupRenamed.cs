using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups;

public record GroupRenamed(int GroupId) : IGroupEvent;
