using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Endpoints.Live;

public class SpotChangedPusher(IHubContext<VenueHub> hub, IVenueDbContext db) : IEventListener
{
    public async Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
    {
        var spotIds = await SpotIdsOfAsync(venueEvent);
        foreach (var spotId in spotIds)
        {
            await hub.Clients.All.SendAsync(VenueHub.SpotChanged, spotId, cancellationToken);
        }
    }


    private async Task<IReadOnlyList<int>> SpotIdsOfAsync(IVenueEvent venueEvent)
    {
        switch (venueEvent)
        {
            case ISpotEvent spotEvent:
                return [spotEvent.SpotId];
            case DeviceDeleted deleted:
                return deleted.UnmappedSpotIds;
            case OutletChanged changed:
                return await db.Spots
                    .Where(s => s.DeviceId == changed.Outlet.DeviceId && s.Channel == changed.Outlet.Channel)
                    .Select(s => s.Id)
                    .ToListAsync();
            case IGroupEvent groupEvent:
                return await db.Spots
                    .Where(s => s.GroupId == groupEvent.GroupId)
                    .OrderBy(s => s.Id)
                    .Select(s => s.Id)
                    .ToListAsync();
            case IDeviceEvent deviceEvent:
                return await db.Spots
                    .Where(s => s.DeviceId == deviceEvent.DeviceId)
                    .OrderBy(s => s.Id)
                    .Select(s => s.Id)
                    .ToListAsync();
            default:
                return [];
        }
    }
}
