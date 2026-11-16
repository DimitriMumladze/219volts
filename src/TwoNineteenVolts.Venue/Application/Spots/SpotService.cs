using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SpotService(
    IVenueDbContext db,
    BoardGate gate,
    ISpotCodeGenerator codes,
    VisibleSpots visibleSpots,
    SwitchFailures failures,
    SessionService sessions,
    IRunningSessions runningSessions,
    IAuditLog audit,
    ICurrentActor currentActor,
    IEventQueue events,
    TimeProvider time,
    ILogger<SpotService> logger)
{
    public const int MaxNameLength = 30;

    private const string None = "none";

    public async Task<Result<Spot>> CreateAsync(string name, int groupId, int? deviceId, int? channel)
    {
        var cleanName = name.Trim();
        var error = ValidateInput(cleanName, deviceId, channel);
        if (error is not null)
        {
            return Fail(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        error = await ValidateGroupAsync(groupId);
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
        var spot = new Spot(code, cleanName, groupId, deviceId, channel, now);

        db.Spots.Add(spot);
        await db.SaveChangesAsync();
        var actor = currentActor.Get();
        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotCreated, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        events.Publish(new SpotCreated(spot.Id));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> UpdateAsync(int id, string name, int groupId, int? deviceId, int? channel)
    {
        var actor = currentActor.Get();
        var spot = await visibleSpots.FindAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        var mappingChanged = spot.DeviceId != deviceId || spot.Channel != channel;
        var groupChanged = spot.GroupId != groupId;
        if ((mappingChanged || groupChanged) && actor.Role != Role.Support)
        {
            return Fail(SpotError.OnlyNameChangeAllowed);
        }

        if (mappingChanged && await runningSessions.OnSpotAsync(id))
        {
            return Fail(SpotError.SessionRunning);
        }

        var cleanName = name.Trim();
        var error = ValidateInput(cleanName, deviceId, channel);
        if (error is not null)
        {
            return Fail(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (groupChanged)
        {
            error = await ValidateGroupAsync(groupId);
            if (error is not null)
            {
                return Fail(error);
            }
        }

        error = await ValidateAgainstOthersAsync(cleanName, deviceId, channel, ignoredSpotId: id);
        if (error is not null)
        {
            return Fail(error);
        }

        var oldGroup = ShowOrNone(spot.GroupId);
        var newGroup = ShowOrNone(groupId);
        var oldDevice = ShowOrNone(spot.DeviceId);
        var newDevice = ShowOrNone(deviceId);
        var oldChannel = ShowOrNone(spot.Channel);
        var newChannel = ShowOrNone(channel);
        var changes = Changes(
            ("name", spot.Name, cleanName),
            ("group", oldGroup, newGroup),
            ("device", oldDevice, newDevice),
            ("channel", oldChannel, newChannel));
        if (changes.Length == 0)
        {
            return Result<Spot>.Ok(spot);
        }

        spot.Rename(cleanName);
        spot.MoveTo(groupId);
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
        await transaction.CommitAsync();

        events.Publish(new SpotUpdated(spot.Id));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> NewCodeAsync(int id)
    {
        var actor = currentActor.Get();
        var spot = await visibleSpots.FindAsync(id, actor);
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

        events.Publish(new SpotCodeChanged(spot.Id));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> SwitchAsync(int id, bool on)
    {
        var actor = currentActor.Get();
        var spot = await visibleSpots.FindAsync(id, actor);
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
        var switched = await gate.RunAsync(device, board => SwitchInsideGateAsync(board, spot.Id, channel, on, actor));
        if (switched.IsFailure)
        {
            await failures.RecordAsync(spot.Id, actor, switched.Error);
            return Fail(switched.Error);
        }

        if (switched.Value != on)
        {
            await failures.RecordAsync(spot.Id, actor, SpotError.StateMismatch);
            return Fail(SpotError.StateMismatch);
        }

        var target = AuditTarget.Spot(spot.Id);

        var action = on ? AuditActions.SpotSwitchedOn : AuditActions.SpotSwitchedOff;
        audit.Record(actor, action, target);
        await db.SaveChangesAsync();
        logger.LogInformation("Spot {SpotId} switched {State} by {Actor}", spot.Id, on ? "on" : "off", actor.Name);

        events.Publish(new SpotSwitched(spot.Id, on));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> ArchiveAsync(int id)
    {
        var actor = currentActor.Get();
        var spot = await visibleSpots.FindAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        if (await runningSessions.OnSpotAsync(id))
        {
            return Fail(SpotError.SessionRunning);
        }

        spot.Archive();
        var target = AuditTarget.Spot(spot.Id);
        audit.Record(actor, AuditActions.SpotArchived, target);
        await db.SaveChangesAsync();

        events.Publish(new SpotArchived(spot.Id));
        return Result<Spot>.Ok(spot);
    }


    private async Task<Result<bool>> SwitchInsideGateAsync(Board board, int spotId, int channel, bool on, AuditActor actor)
    {
        var switched = await board.SwitchAsync(channel, on);
        if (switched.IsFailure || switched.Value != on || on)
        {
            return switched;
        }

        await sessions.EndRunningInsideGateAsync(spotId, SessionEndReason.ManualOff, actor);
        return switched;
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

    private async Task<string?> ValidateGroupAsync(int groupId)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == groupId);
        if (group is null)
        {
            return SpotError.UnknownGroup;
        }

        if (group.IsArchived)
        {
            return SpotError.GroupArchived;
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
