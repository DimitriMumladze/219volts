using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotResponses
{
    public static async Task<SpotResponse> BuildAsync(Spot spot, IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings)
    {
        List<Spot> spots = [spot];
        var responses = await BuildAllAsync(spots, db, statuses, warnings);
        return responses[0];
    }

    public static async Task<List<SpotResponse>> BuildAllAsync(List<Spot> spots, IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings)
    {
        var spotIds = spots.Select(s => s.Id).ToList();
        var deviceIds = spots.Select(s => s.DeviceId).ToList();
        var groupIds = spots.Select(s => s.GroupId).ToList();

        var devices = await db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);
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

        var context = new Lookups(devices, groups, running, totals, statuses, warnings);
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

        var device = context.Devices[deviceId];
        var status = context.Statuses.Get(deviceId);
        var isOn = status.IsOn(channel);
        var switchOffFailed = context.Warnings.Has(spot.Id) && isOn != false;

        return new SpotResponse(
            spot.Id,
            spot.Code,
            spot.Name,
            group.Id,
            group.Name,
            deviceId,
            device.Name,
            channel,
            isOn,
            status.IsOnline,
            status.LastSeen,
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
        Dictionary<int, Device> Devices,
        Dictionary<int, Group> Groups,
        Dictionary<int, Session> Running,
        Dictionary<int, long> Totals,
        DeviceStatuses Statuses,
        SwitchOffWarnings Warnings);
}
