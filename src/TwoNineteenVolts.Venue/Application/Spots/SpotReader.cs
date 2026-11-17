using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots.Contracts;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SpotReader(IVenueDbContext db, IDeviceGateway devices, SwitchOffWarnings warnings)
{
    public Task<Spot?> FindVisibleAsync(int id, AuditActor actor)
    {
        return VisibleTo(actor).SingleOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SpotView?> ViewAsync(int id, AuditActor actor)
    {
        var spot = await FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return null;
        }

        List<Spot> spots = [spot];
        var views = await ViewsOfAsync(spots);
        return views[0];
    }

    public async Task<List<SpotView>> ViewAllAsync(AuditActor actor)
    {
        var spots = await VisibleTo(actor)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return await ViewsOfAsync(spots);
    }


    private IQueryable<Spot> VisibleTo(AuditActor actor)
    {
        var spots = db.Spots.AsQueryable();
        if (actor.Role != Role.Support)
        {
            spots = spots.Where(s => s.DeviceId != null && !s.IsArchived);
        }

        return spots;
    }

    private async Task<List<SpotView>> ViewsOfAsync(List<Spot> spots)
    {
        var spotIds = spots.Select(s => s.Id).ToList();
        var groupIds = spots.Select(s => s.GroupId).ToList();
        var outlets = spots
            .Where(s => s.DeviceId != null && s.Channel != null)
            .Select(s => new Outlet(s.DeviceId!.Value, s.Channel!.Value))
            .ToList();

        var states = await devices.StatesOfAsync(outlets);
        var groups = await db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id);
        var running = await db.Sessions
            .Where(s => s.EndedAt == null && spotIds.Contains(s.SpotId))
            .ToDictionaryAsync(s => s.SpotId);
        var sessionIds = running.Values.Select(s => s.Id).ToList();
        var totals = await db.SessionCharges
            .Where(c => sessionIds.Contains(c.SessionId))
            .GroupBy(c => c.SessionId)
            .Select(g => new { SessionId = g.Key, Total = g.Sum(c => c.ChargedTetri) })
            .ToDictionaryAsync(t => t.SessionId, t => t.Total);

        var lookups = new Lookups(states, groups, running, totals);
        return spots.Select(s => ViewOf(s, lookups)).ToList();
    }

    private SpotView ViewOf(Spot spot, Lookups lookups)
    {
        var group = lookups.Groups[spot.GroupId];
        var session = SessionOf(spot, lookups);
        var state = StateOf(spot, lookups);
        if (state is null)
        {
            return new SpotView(spot.Id, spot.Code, spot.Name, group.Id, group.Name, null, null, null, null, null, null, spot.IsArchived, spot.CreatedAt, session, false);
        }

        var isKnownOff = state.IsOn == false;
        var hasSwitchOffFailed = warnings.HasWarning(spot.Id) && !isKnownOff;

        return new SpotView(
            spot.Id,
            spot.Code,
            spot.Name,
            group.Id,
            group.Name,
            spot.DeviceId,
            state.DeviceName,
            spot.Channel,
            state.IsOn,
            state.IsDeviceOnline,
            state.LastSeen,
            spot.IsArchived,
            spot.CreatedAt,
            session,
            hasSwitchOffFailed);
    }

    private static OutletState? StateOf(Spot spot, Lookups lookups)
    {
        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return null;
        }

        var outlet = new Outlet(deviceId, channel);
        return lookups.States.GetValueOrDefault(outlet);
    }

    private static SpotSessionView? SessionOf(Spot spot, Lookups lookups)
    {
        if (!lookups.Running.TryGetValue(spot.Id, out var session))
        {
            return null;
        }

        var total = lookups.Totals.GetValueOrDefault(session.Id);
        return new SpotSessionView(session.Id, session.StartedAt, session.EndsAt, session.StartedByName, total);
    }

    private record Lookups(
        IReadOnlyDictionary<Outlet, OutletState> States,
        Dictionary<int, Group> Groups,
        Dictionary<int, Session> Running,
        Dictionary<int, long> Totals);
}
