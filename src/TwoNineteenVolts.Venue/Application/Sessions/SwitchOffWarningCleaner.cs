using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public class SwitchOffWarningCleaner(IVenueDbContext db, SwitchOffWarnings warnings) : IEventListener
{
    public async Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
    {
        if (venueEvent is not ChannelStateChanged changed || changed.IsOn != false)
        {
            return;
        }

        var spotIds = await db.Spots
            .Where(s => s.DeviceId == changed.DeviceId && s.Channel == changed.Channel)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        foreach (var spotId in spotIds)
        {
            warnings.Clear(spotId);
        }
    }
}
