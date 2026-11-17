namespace TwoNineteenVolts.Venue.Application.Sessions;

public interface IRunningSessions
{
    Task<bool> HasSessionOnSpotAsync(int spotId);

    Task<bool> HasSessionOnDeviceAsync(int deviceId);
}
