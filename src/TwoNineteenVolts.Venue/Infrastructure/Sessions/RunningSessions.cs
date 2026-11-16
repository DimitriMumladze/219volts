using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public class RunningSessions(VenueDbContext db) : IRunningSessions
{
    public Task<bool> OnSpotAsync(int spotId)
    {
        return db.Sessions.AnyAsync(s => s.SpotId == spotId && s.EndedAt == null);
    }

    public Task<bool> OnDeviceAsync(int deviceId)
    {
        var spotsOnDevice = db.Spots
            .Where(s => s.DeviceId == deviceId)
            .Select(s => s.Id);

        return db.Sessions.AnyAsync(s => s.EndedAt == null && spotsOnDevice.Contains(s.SpotId));
    }
}
