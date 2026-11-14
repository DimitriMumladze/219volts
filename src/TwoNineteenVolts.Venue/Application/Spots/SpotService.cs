using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SpotService(
    IVenueDbContext db,
    RelayDrivers drivers,
    DeviceStatuses statuses,
    ISpotCodeGenerator codes,
    IAuditLog audit,
    ICurrentActor currentActor,
    TimeProvider time,
    ILogger<SpotService> logger)
{
    public const int MaxNameLength = 30;

    private const string None = "none";
    private const string NoAnswerReason = "no answer";
    private const string ErrorAnswerReason = "error answer";
    private const string MismatchReason = "mismatch";

    public async Task<Result<Spot>> CreateAsync(string name, int? deviceId, int? channel)
    {
        var cleanName = name.Trim();
        var error = ValidateInput(cleanName, deviceId, channel);
        if (error is not null)
        {
            return Fail(error);
        }

        error = await ValidateAgainstOthersAsync(cleanName, deviceId, channel, ignoredSpotId: null);
        if (error is not null)
        {
            return Fail(error);
        }

        var code = codes.NewCode();
        if (await IsCodeTakenAsync(code))
        {
            return Fail(SpotError.CodeTaken);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var spot = new Spot(code, cleanName, deviceId, channel, now);

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Spots.Add(spot);
        await db.SaveChangesAsync();
        var actor = currentActor.Get();
        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotCreated, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> UpdateAsync(int id, string name, int? deviceId, int? channel)
    {
        var actor = currentActor.Get();
        var spot = await FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        var mappingChanged = spot.DeviceId != deviceId || spot.Channel != channel;
        if (mappingChanged && actor.Role != Role.Support)
        {
            return Fail(SpotError.OnlyNameChangeAllowed);
        }

        var cleanName = name.Trim();
        var error = ValidateInput(cleanName, deviceId, channel);
        if (error is not null)
        {
            return Fail(error);
        }

        error = await ValidateAgainstOthersAsync(cleanName, deviceId, channel, ignoredSpotId: id);
        if (error is not null)
        {
            return Fail(error);
        }

        var oldDevice = ShowOrNone(spot.DeviceId);
        var newDevice = ShowOrNone(deviceId);
        var oldChannel = ShowOrNone(spot.Channel);
        var newChannel = ShowOrNone(channel);
        var changes = Changes(
            ("name", spot.Name, cleanName),
            ("device", oldDevice, newDevice),
            ("channel", oldChannel, newChannel));
        if (changes.Length == 0)
        {
            return Result<Spot>.Ok(spot);
        }

        spot.Rename(cleanName);
        if (deviceId is int mappedDeviceId && channel is int mappedChannel)
        {
            spot.MapTo(mappedDeviceId, mappedChannel);
        }
        else
        {
            spot.Unmap();
        }

        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotUpdated, target, changes);
        await db.SaveChangesAsync();

        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> NewCodeAsync(int id)
    {
        var actor = currentActor.Get();
        var spot = await FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        var code = codes.NewCode();
        if (await IsCodeTakenAsync(code))
        {
            return Fail(SpotError.CodeTaken);
        }

        var oldCode = spot.Code;
        spot.ChangeCode(code);
        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotCodeChanged, target, $"{oldCode} → {code}");
        await db.SaveChangesAsync();

        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> SwitchAsync(int id, bool on)
    {
        var actor = currentActor.Get();
        var spot = await FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return Fail(SpotError.Unmapped);
        }

        var device = await db.Devices.SingleAsync(d => d.Id == deviceId);
        var driver = drivers.For(device.Kind);

        var set = await SetAsync(driver, device, channel, on);
        if (set.IsFailure)
        {
            return await FailSwitchAsync(spot, deviceId, channel, actor, set.Error);
        }

        var read = await ReadAsync(driver, device, channel);
        if (read.IsFailure)
        {
            return await FailSwitchAsync(spot, deviceId, channel, actor, read.Error);
        }

        var target = AuditTarget.Spot(spot.Id);
        statuses.StoreChannel(deviceId, device.ChannelCount, channel, read.Value);
        if (read.Value != on)
        {
            logger.LogWarning("Switching spot {SpotId} failed: the relay reads {State}", spot.Id, read.Value);
            audit.Record(actor, AuditActions.SpotSwitchFailed, target, MismatchReason);
            await db.SaveChangesAsync();
            return Fail(SpotError.StateMismatch);
        }

        var action = on ? AuditActions.SpotSwitchedOn : AuditActions.SpotSwitchedOff;
        audit.Record(actor, action, target);
        await db.SaveChangesAsync();
        logger.LogInformation("Spot {SpotId} switched {State} by {Actor}", spot.Id, on ? "on" : "off", actor.Name);

        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> ArchiveAsync(int id)
    {
        var actor = currentActor.Get();
        var spot = await FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        spot.Archive();
        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotArchived, target);
        await db.SaveChangesAsync();

        return Result<Spot>.Ok(spot);
    }


    private async Task<Spot?> FindVisibleAsync(int id, AuditActor actor)
    {
        var spot = await db.Spots.SingleOrDefaultAsync(s => s.Id == id);
        if (spot is null)
        {
            return null;
        }

        var hidden = !spot.IsMapped || spot.IsArchived;
        if (hidden && actor.Role != Role.Support)
        {
            return null;
        }

        return spot;
    }

    private async Task<Result<bool>> SetAsync(IRelayDriver driver, Device device, int channel, bool on)
    {
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        var result = await driver.SetAsync(device, channel, on, timeout.Token);
        statuses.RecordOutcome(device.Id, result.Error);
        return result;
    }

    private async Task<Result<bool>> ReadAsync(IRelayDriver driver, Device device, int channel)
    {
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        var result = await driver.ReadAsync(device, channel, timeout.Token);
        statuses.RecordOutcome(device.Id, result.Error);
        return result;
    }

    private async Task<Result<Spot>> FailSwitchAsync(Spot spot, int deviceId, int channel, AuditActor actor, string error)
    {
        var reason = ReasonFor(error);
        statuses.ForgetChannel(deviceId, channel);
        logger.LogWarning("Switching spot {SpotId} failed: {Reason}", spot.Id, reason);

        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotSwitchFailed, target, reason);
        await db.SaveChangesAsync();

        return Fail(error);
    }

    private static string ReasonFor(string error)
    {
        if (error == DeviceError.NoAnswer)
        {
            return NoAnswerReason;
        }

        return ErrorAnswerReason;
    }

    private static string? ValidateInput(string name, int? deviceId, int? channel)
    {
        if (name.Length == 0)
        {
            return SpotError.InvalidName;
        }

        if (name.Length > MaxNameLength)
        {
            return SpotError.NameTooLong;
        }

        var hasDevice = deviceId is not null;
        var hasChannel = channel is not null;
        if (hasDevice != hasChannel)
        {
            return SpotError.InvalidMapping;
        }

        return null;
    }

    private async Task<string?> ValidateAgainstOthersAsync(string name, int? deviceId, int? channel, int? ignoredSpotId)
    {
        if (deviceId is int mappedDeviceId && channel is int mappedChannel)
        {
            var device = await db.Devices.SingleOrDefaultAsync(d => d.Id == mappedDeviceId);
            if (device is null)
            {
                return SpotError.UnknownDevice;
            }

            if (mappedChannel < 1 || mappedChannel > device.ChannelCount)
            {
                return SpotError.ChannelOutOfRange;
            }

            if (await db.Spots.AnyAsync(s => s.DeviceId == mappedDeviceId && s.Channel == mappedChannel && s.Id != ignoredSpotId))
            {
                return SpotError.ChannelInUse;
            }
        }

        var key = name.ToUpperInvariant();
        if (await db.Spots.AnyAsync(s => s.NameKey == key && s.Id != ignoredSpotId))
        {
            return SpotError.DuplicateName;
        }

        return null;
    }

    private Task<bool> IsCodeTakenAsync(string code)
    {
        return db.Spots.AnyAsync(s => s.Code == code);
    }

    private static string ShowOrNone(int? value)
    {
        if (value is int number)
        {
            return number.ToString();
        }

        return None;
    }

    private static string Changes(params (string Field, string Old, string New)[] fields)
    {
        var changed = fields
            .Where(f => f.Old != f.New)
            .Select(f => $"{f.Field}: {f.Old} → {f.New}");

        return string.Join("; ", changed);
    }

    private static Result<Spot> Fail(string error)
    {
        return Result<Spot>.Fail(error);
    }
}
