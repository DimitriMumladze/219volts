using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups.Events;

public record GroupCreated(int GroupId) : IGroupEvent;
