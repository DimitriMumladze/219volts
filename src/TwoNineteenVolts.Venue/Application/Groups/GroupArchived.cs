using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups;

public record GroupArchived(int GroupId) : IGroupEvent;
