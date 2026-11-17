using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotResponses
{
    public static async Task<SpotResponse> BuildAsync(Spot spot, IVenueDbContext db, IDeviceGateway devices, SwitchOffWarnings warnings)
    {
        List<Spot> spots = [spot];
        var responses = await BuildAllAsync(spots, db, devices, warnings);
        return responses[0];
    }

    public static async Task<List<SpotResponse>> BuildAllAsync(List<Spot> spots, IVenueDbContext db, IDeviceGateway devices, SwitchOffWarnings warnings)
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

        var context = new Lookups(states, groups, running, totals, warnings);
        return spots.Select(s => Build(s, context)).ToList();
    }


    private static SpotResponse Build(Spot spot, Lookups context)
    {
        var group = context.Groups[spot.GroupId];
        var session = SessionOf(spot, context);
        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return new SpotResponse(spot.Id, spot.Code, spot.Name, group.Id, group.Name, null, null, null, null, null, null, spot.IsArchived, spot.CreatedAt, session, false);
        }

        var outlet = new Outlet(deviceId, channel);
        if (!context.States.TryGetValue(outlet, out var state))
        {
            return new SpotResponse(spot.Id, spot.Code, spot.Name, group.Id, group.Name, null, null, null, null, null, null, spot.IsArchived, spot.CreatedAt, session, false);
        }

        var switchOffFailed = context.Warnings.Has(spot.Id) && state.IsOn != false;

        return new SpotResponse(
            spot.Id,
            spot.Code,
            spot.Name,
            group.Id,
            group.Name,
            deviceId,
            state.DeviceName,
            channel,
            state.IsOn,
            state.DeviceOnline,
            state.LastSeen,
            spot.IsArchived,
            spot.CreatedAt,
            session,
            switchOffFailed);
    }

    private static SpotSessionResponse? SessionOf(Spot spot, Lookups context)
    {
        if (!context.Running.TryGetValue(spot.Id, out var session))
        {
            return null;
        }

        var totalTetri = context.Totals.GetValueOrDefault(session.Id);
        var total = Money.ToGel(totalTetri);
        return new SpotSessionResponse(session.Id, session.StartedAt, session.EndsAt, session.StartedByName, total);
    }

    private record Lookups(
        IReadOnlyDictionary<Outlet, OutletState> States,
        Dictionary<int, Group> Groups,
        Dictionary<int, Session> Running,
        Dictionary<int, long> Totals,
        SwitchOffWarnings Warnings);
}
