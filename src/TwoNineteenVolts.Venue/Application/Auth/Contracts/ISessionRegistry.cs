namespace TwoNineteenVolts.Venue.Application.Auth.Contracts;

public interface ISessionRegistry
{
    void EndSessionsFor(int userId);
}
