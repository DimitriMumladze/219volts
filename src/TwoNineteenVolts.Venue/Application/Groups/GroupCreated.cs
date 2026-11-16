using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups;

public record GroupCreated(int GroupId) : IGroupEvent;
