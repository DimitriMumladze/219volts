namespace TwoNineteenVolts.Venue.Application.Events;

public interface IGroupEvent : IVenueEvent
{
    int GroupId { get; }
}
