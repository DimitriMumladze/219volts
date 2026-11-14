namespace TwoNineteenVolts.Venue.Application.Auth;

public interface ISessionRegistry
{
    void EndSessionsFor(int userId);
}
