using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Devices.Events;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Sessions.Contracts;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class DeviceService(
    IVenueDbContext db,
    RelayDrivers drivers,
    DeviceStatuses statuses,
    DeviceLocks locks,
    IAuditLog audit,
    ICurrentActor currentActor,
    IEventQueue events,
    IRunningSessions runningSessions,
    TimeProvider time)
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;
    private const string None = "none";

    public async Task<Result<Device>> RegisterAsync(string name, DeviceKind kind, string ipAddress, int? port, int channelCount)
    {
        var driver = drivers.For(kind);
        var cleanName = name.Trim();
        var cleanPort = port ?? driver.DefaultPort;

        if (!driver.AllowedChannelCounts.Contains(channelCount))
        {
            return Fail(DeviceError.InvalidChannelCount);
        }

        var validation = await ValidateAsync(cleanName, ipAddress, cleanPort, ignoredDeviceId: null);
        if (validation.IsFailure)
        {
            return Fail(validation.Error);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var device = new Device(cleanName, kind, ipAddress, cleanPort, channelCount, now);

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var actor = currentActor.Get();
        var target = TargetOf(device);
        audit.Record(actor, AuditActions.DeviceRegistered, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        events.Publish(new DeviceRegistered(device.Id));
        return Result<Device>.Ok(device);
    }

    public async Task<Result<Device>> UpdateAsync(int id, string name, string ipAddress, int port)
    {
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id);
        if (device is null)
        {
            return Fail(DeviceError.NotFound);
        }

        var cleanName = name.Trim();
        var validation = await ValidateAsync(cleanName, ipAddress, port, ignoredDeviceId: id);
        if (validation.IsFailure)
        {
            return Fail(validation.Error);
        }

        var oldPort = device.Port.ToString();
        var newPort = port.ToString();
        var changes = AuditChanges.Fields(
            ("name", device.Name, cleanName),
            ("ip", device.IpAddress, ipAddress),
            ("port", oldPort, newPort));
        var hasAddressChanged = device.IpAddress != ipAddress || device.Port != port;
        if (changes.Length == 0)
        {
            return Result<Device>.Ok(device);
        }

        device.Update(cleanName, ipAddress, port);
        var actor = currentActor.Get();
        var target = TargetOf(device);
        audit.Record(actor, AuditActions.DeviceUpdated, target, changes);
        await db.SaveChangesAsync();

        if (hasAddressChanged)
        {
            statuses.Remove(id);
        }

        events.Publish(new DeviceUpdated(id));
        return Result<Device>.Ok(device);
    }

    public async Task<Result<Device>> DeleteAsync(int id)
    {
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id);
        if (device is null)
        {
            return Fail(DeviceError.NotFound);
        }

        if (await runningSessions.HasSessionOnDeviceAsync(id))
        {
            return Fail(DeviceError.SessionRunning);
        }

        var spots = await db.Spots
            .Where(s => s.DeviceId == id)
            .OrderBy(s => s.Id)
            .ToListAsync();
        foreach (var spot in spots)
        {
            spot.Unmap();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        db.Devices.Remove(device);
        var unmapped = UnmappedSpotsOf(spots);
        var actor = currentActor.Get();
        var target = TargetOf(device);
        audit.Record(actor, AuditActions.DeviceDeleted, target, $"unmapped spots: {unmapped}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        statuses.Remove(id);
        locks.Remove(id);
        var unmappedSpotIds = spots.Select(s => s.Id).ToList();
        events.Publish(new DeviceDeleted(id, unmappedSpotIds));
        return Result<Device>.Ok(device);
    }


    private async Task<Result> ValidateAsync(string name, string ipAddress, int port, int? ignoredDeviceId)
    {
        if (name.Length == 0)
        {
            return Result.Fail(DeviceError.InvalidName);
        }

        if (!IsIPv4(ipAddress))
        {
            return Result.Fail(DeviceError.InvalidIpAddress);
        }

        if (port < MinPort || port > MaxPort)
        {
            return Result.Fail(DeviceError.InvalidPort);
        }

        var key = name.ToUpperInvariant();
        if (await db.Devices.AnyAsync(d => d.NameKey == key && d.Id != ignoredDeviceId))
        {
            return Result.Fail(DeviceError.DuplicateName);
        }

        if (await db.Devices.AnyAsync(d => d.IpAddress == ipAddress && d.Port == port && d.Id != ignoredDeviceId))
        {
            return Result.Fail(DeviceError.DuplicateAddress);
        }

        return Result.Ok();
    }

    private static bool IsIPv4(string ipAddress) =>
        IPAddress.TryParse(ipAddress, out var parsed)
        && parsed.AddressFamily == AddressFamily.InterNetwork
        && parsed.ToString() == ipAddress;

    private static string UnmappedSpotsOf(List<Spot> spots)
    {
        if (spots.Count == 0)
        {
            return None;
        }

        var spotTargets = spots.Select(s => AuditTarget.Spot(s.Id));
        return string.Join(", ", spotTargets);
    }

    private static string TargetOf(Device device) => AuditTarget.Device(device.Id);

    private static Result<Device> Fail(string error) => Result<Device>.Fail(error);
}
