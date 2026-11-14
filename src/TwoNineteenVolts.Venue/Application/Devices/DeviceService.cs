using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

// Support registers, edits and deletes the venue's relay devices.
public class DeviceService(
    IVenueDbContext db,
    RelayDrivers drivers,
    DeviceStatuses statuses,
    IAuditLog audit,
    ICurrentActor currentActor,
    TimeProvider time)
{
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    public async Task<Result<Device>> RegisterAsync(string name, DeviceKind kind, string ipAddress, int? port, int channelCount)
    {
        var driver = drivers.For(kind);
        var cleanName = name.Trim();
        var cleanPort = port ?? driver.DefaultPort;

        var error = await ValidateAsync(cleanName, ipAddress, cleanPort, ignoredDeviceId: null);
        if (error is not null)
        {
            return Fail(error);
        }

        if (!driver.AllowedChannelCounts.Contains(channelCount))
        {
            return Fail(DeviceError.InvalidChannelCount);
        }

        var device = new Device(cleanName, kind, ipAddress, cleanPort, channelCount, time.GetUtcNow().UtcDateTime);

        // The audit entry targets the new id, which SQLite assigns only on insert,
        // so the device and its entry are saved in two steps inside one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        audit.Record(currentActor.Get(), AuditActions.DeviceRegistered, TargetOf(device));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

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
        var error = await ValidateAsync(cleanName, ipAddress, port, ignoredDeviceId: id);
        if (error is not null)
        {
            return Fail(error);
        }

        var changes = Changes(
            ("name", device.Name, cleanName),
            ("ip", device.IpAddress, ipAddress),
            ("port", device.Port.ToString(), port.ToString()));
        var addressChanged = device.IpAddress != ipAddress || device.Port != port;

        device.Update(cleanName, ipAddress, port);
        audit.Record(currentActor.Get(), AuditActions.DeviceUpdated, TargetOf(device), changes);
        await db.SaveChangesAsync();

        // The old answers say nothing about the new address.
        if (addressChanged)
        {
            statuses.Reset(id);
        }

        return Result<Device>.Ok(device);
    }

    // Spots on the device stay, unmapped, until Support maps them again.
    public async Task<Result<Device>> DeleteAsync(int id)
    {
        var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == id);
        if (device is null)
        {
            return Fail(DeviceError.NotFound);
        }

        var spots = await db.Spots
            .Where(s => s.DeviceId == id)
            .OrderBy(s => s.Id)
            .ToListAsync();
        foreach (var spot in spots)
        {
            spot.Unmap();
        }

        // Spots first: the database refuses to delete a device that spots still point to.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.SaveChangesAsync();
        db.Devices.Remove(device);
        var unmapped = string.Join(", ", spots.Select(s => $"spot:{s.Id}"));
        audit.Record(currentActor.Get(), AuditActions.DeviceDeleted, TargetOf(device), $"unmapped spots: {unmapped}");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        statuses.Remove(id);
        return Result<Device>.Ok(device);
    }

    private async Task<string?> ValidateAsync(string name, string ipAddress, int port, int? ignoredDeviceId)
    {
        if (name.Length == 0)
        {
            return DeviceError.InvalidName;
        }

        if (!IsIPv4(ipAddress))
        {
            return DeviceError.InvalidIpAddress;
        }

        if (port < MinPort || port > MaxPort)
        {
            return DeviceError.InvalidPort;
        }

        // Name uses the NOCASE collation, so this comparison ignores case.
        if (await db.Devices.AnyAsync(d => d.Name == name && d.Id != ignoredDeviceId))
        {
            return DeviceError.DuplicateName;
        }

        if (await db.Devices.AnyAsync(d => d.IpAddress == ipAddress && d.Port == port && d.Id != ignoredDeviceId))
        {
            return DeviceError.DuplicateAddress;
        }

        return null;
    }

    // Only the dotted four-number form: IPAddress.TryParse alone also accepts "192.168.1" and "1".
    private static bool IsIPv4(string ipAddress) =>
        IPAddress.TryParse(ipAddress, out var parsed)
        && parsed.AddressFamily == AddressFamily.InterNetwork
        && parsed.ToString() == ipAddress;

    // "name: Panel 1 → Panel A; ip: 192.168.1.20 → 192.168.1.21", only the fields that changed.
    private static string Changes(params (string Field, string Old, string New)[] fields) =>
        string.Join("; ", fields
            .Where(f => f.Old != f.New)
            .Select(f => $"{f.Field}: {f.Old} → {f.New}"));

    private static string TargetOf(Device device) => $"device:{device.Id}";

    private static Result<Device> Fail(string error) => Result<Device>.Fail(error);
}
