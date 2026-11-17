using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Groups.Events;

public record GroupRateChanged(int GroupId) : IGroupEvent;
