using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SpotService(
    IVenueDbContext db,
    IDeviceGateway devices,
    ISpotCodeGenerator codes,
    SpotReader reader,
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
        var inputValidation = ValidateInput(cleanName, deviceId, channel);
        if (inputValidation.IsFailure)
        {
            return Fail(inputValidation.Error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        var groupValidation = await ValidateGroupAsync(groupId);
        if (groupValidation.IsFailure)
        {
            return Fail(groupValidation.Error);
        }

        var othersValidation = await ValidateAgainstOthersAsync(cleanName, deviceId, channel, ignoredSpotId: null);
        if (othersValidation.IsFailure)
        {
            return Fail(othersValidation.Error);
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
        var spot = await reader.FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        var hasMappingChanged = spot.DeviceId != deviceId || spot.Channel != channel;
        var hasGroupChanged = spot.GroupId != groupId;
        if ((hasMappingChanged || hasGroupChanged) && actor.Role != Role.Support)
        {
            return Fail(SpotError.OnlyNameChangeAllowed);
        }

        if (hasMappingChanged && await runningSessions.HasSessionOnSpotAsync(id))
        {
            return Fail(SpotError.SessionRunning);
        }

        var cleanName = name.Trim();
        var inputValidation = ValidateInput(cleanName, deviceId, channel);
        if (inputValidation.IsFailure)
        {
            return Fail(inputValidation.Error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (hasGroupChanged)
        {
            var groupValidation = await ValidateGroupAsync(groupId);
            if (groupValidation.IsFailure)
            {
                return Fail(groupValidation.Error);
            }
        }

        var othersValidation = await ValidateAgainstOthersAsync(cleanName, deviceId, channel, ignoredSpotId: id);
        if (othersValidation.IsFailure)
        {
            return Fail(othersValidation.Error);
        }

        var oldGroup = ShowOrNone(spot.GroupId);
        var newGroup = ShowOrNone(groupId);
        var oldDevice = ShowOrNone(spot.DeviceId);
        var newDevice = ShowOrNone(deviceId);
        var oldChannel = ShowOrNone(spot.Channel);
        var newChannel = ShowOrNone(channel);
        var changes = AuditChanges.Fields(
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
        var spot = await reader.FindVisibleAsync(id, actor);
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
        var details = AuditChanges.Change(oldCode, code);
        audit.Record(actor, AuditActions.SpotCodeChanged, target, details);
        await db.SaveChangesAsync();

        events.Publish(new SpotCodeChanged(spot.Id));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> SwitchAsync(int id, bool shouldBeOn)
    {
        var actor = currentActor.Get();
        var spot = await reader.FindVisibleAsync(id, actor);
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

        var outlet = new Outlet(deviceId, channel);
        var switched = await SwitchOutletAsync(outlet, spot.Id, shouldBeOn, actor);
        if (switched.IsFailure)
        {
            await failures.RecordAsync(spot.Id, actor, switched.Error);
            var spotError = SwitchFailures.ToSpotError(switched.Error);
            return Fail(spotError);
        }

        var target = AuditTarget.Spot(spot.Id);

        var action = shouldBeOn ? AuditActions.SpotSwitchedOn : AuditActions.SpotSwitchedOff;
        audit.Record(actor, action, target);
        await db.SaveChangesAsync();
        var stateText = shouldBeOn ? "on" : "off";
        logger.LogInformation("Spot {SpotId} switched {State} by {Actor}", spot.Id, stateText, actor.Name);

        events.Publish(new SpotSwitched(spot.Id, shouldBeOn));
        return Result<Spot>.Ok(spot);
    }

    public async Task<Result<Spot>> ArchiveAsync(int id)
    {
        var actor = currentActor.Get();
        var spot = await reader.FindVisibleAsync(id, actor);
        if (spot is null)
        {
            return Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Fail(SpotError.Archived);
        }

        if (await runningSessions.HasSessionOnSpotAsync(id))
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


    private Task<Result> SwitchOutletAsync(Outlet outlet, int spotId, bool shouldBeOn, AuditActor actor)
    {
        if (shouldBeOn)
        {
            return devices.SwitchAsync(outlet, shouldBeOn: true);
        }

        return sessions.SwitchOffAndEndAsync(outlet, spotId, actor);
    }

    private static Result ValidateInput(string name, int? deviceId, int? channel)
    {
        if (name.Length == 0)
        {
            return Result.Fail(SpotError.InvalidName);
        }

        if (name.Length > MaxNameLength)
        {
            return Result.Fail(SpotError.NameTooLong);
        }

        var hasDevice = deviceId is not null;
        var hasChannel = channel is not null;
        if (hasDevice != hasChannel)
        {
            return Result.Fail(SpotError.InvalidMapping);
        }

        return Result.Ok();
    }

    private async Task<Result> ValidateGroupAsync(int groupId)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == groupId);
        if (group is null)
        {
            return Result.Fail(SpotError.UnknownGroup);
        }

        if (group.IsArchived)
        {
            return Result.Fail(SpotError.GroupArchived);
        }

        return Result.Ok();
    }

    private async Task<Result> ValidateAgainstOthersAsync(string name, int? deviceId, int? channel, int? ignoredSpotId)
    {
        if (deviceId is int mappedDeviceId && channel is int mappedChannel)
        {
            var outlet = new Outlet(mappedDeviceId, mappedChannel);
            var check = await devices.CheckOutletAsync(outlet);
            if (check == OutletCheck.UnknownDevice)
            {
                return Result.Fail(SpotError.UnknownDevice);
            }

            if (check == OutletCheck.ChannelOutOfRange)
            {
                return Result.Fail(SpotError.ChannelOutOfRange);
            }

            if (await db.Spots.AnyAsync(s => s.DeviceId == mappedDeviceId && s.Channel == mappedChannel && s.Id != ignoredSpotId))
            {
                return Result.Fail(SpotError.ChannelInUse);
            }
        }

        var key = name.ToUpperInvariant();
        if (await db.Spots.AnyAsync(s => s.NameKey == key && s.Id != ignoredSpotId))
        {
            return Result.Fail(SpotError.DuplicateName);
        }

        return Result.Ok();
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

    private static Result<Spot> Fail(string error)
    {
        return Result<Spot>.Fail(error);
    }
}
