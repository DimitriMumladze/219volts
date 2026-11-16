namespace TwoNineteenVolts.Venue.Application.Sessions;

public interface IRunningSessions
{
    Task<bool> OnSpotAsync(int spotId);

    Task<bool> OnDeviceAsync(int deviceId);
}
