namespace TwoNineteenVolts.Venue.Application.Sessions.Contracts;

public interface IRunningSessions
{
    Task<bool> HasSessionOnSpotAsync(int spotId);

    Task<bool> HasSessionOnDeviceAsync(int deviceId);
}
