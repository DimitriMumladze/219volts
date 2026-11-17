using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups.Events;

public record GroupArchived(int GroupId) : IGroupEvent;
