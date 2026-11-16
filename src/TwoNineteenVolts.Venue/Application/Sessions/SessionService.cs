using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public class SessionService(
    IVenueDbContext db,
    VisibleSpots visibleSpots,
    BoardGate gate,
    DeviceStatuses statuses,
    SwitchFailures failures,
    SwitchOffWarnings warnings,
    IAuditLog audit,
    ICurrentActor currentActor,
    IEventQueue events,
    TimeProvider time)
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 600;

    private const string TimeUpReason = "time up";
    private const string ManualOffReason = "manual off";

    public async Task<Result<long>> PriceAsync(int spotId, int minutes)
    {
        if (!AreValid(minutes))
        {
            return Result<long>.Fail(SessionError.InvalidMinutes);
        }

        var actor = currentActor.Get();
        var found = await FindTargetAsync(spotId, actor);
        if (found.IsFailure)
        {
            return Result<long>.Fail(found.Error);
        }

        var price = Money.PriceTetri(minutes, found.Value.HourlyRateTetri);
        return Result<long>.Ok(price);
    }

    public async Task<Result<Session>> StartAsync(int spotId, int minutes, decimal? amount)
    {
        var actor = currentActor.Get();
        var prepared = await PrepareChargeAsync(spotId, minutes, amount, actor);
        if (prepared.IsFailure)
        {
            return Fail(prepared.Error);
        }

        var charge = prepared.Value;
        var started = await gate.RunAsync(charge.Target.Device, board => StartInsideGateAsync(board, charge, actor));
        if (started.IsFailure)
        {
            return started;
        }

        var session = started.Value;
        warnings.Clear(spotId);
        events.Publish(new SessionStarted(spotId, session.Id));
        return started;
    }

    public async Task<Result<Session>> ExtendAsync(int spotId, int minutes, decimal? amount)
    {
        var actor = currentActor.Get();
        var prepared = await PrepareChargeAsync(spotId, minutes, amount, actor);
        if (prepared.IsFailure)
        {
            return Fail(prepared.Error);
        }

        var charge = prepared.Value;
        var extended = await gate.RunAsync(charge.Target.Device, _ => ExtendInsideGateAsync(charge, actor));
        if (extended.IsFailure)
        {
            return extended;
        }

        var session = extended.Value;
        events.Publish(new SessionExtended(spotId, session.Id));
        return extended;
    }

    public async Task<Result<Session>> StopAsync(int spotId)
    {
        var actor = currentActor.Get();
        var found = await FindTargetAsync(spotId, actor);
        if (found.IsFailure)
        {
            return Fail(found.Error);
        }

        var target = found.Value;
        return await gate.RunAsync(target.Device, board => StopInsideGateAsync(board, target, actor));
    }

    public async Task EndDueAsync(AuditActor actor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var due = await db.Sessions
            .AsNoTracking()
            .Where(s => s.EndedAt == null && s.EndsAt <= now)
            .OrderBy(s => s.EndsAt)
            .Select(s => s.SpotId)
            .ToListAsync();

        foreach (var spotId in due)
        {
            await EndDueOnSpotAsync(spotId, actor);
        }
    }

    public async Task RestoreDriftedAsync()
    {
        var running = await db.Sessions
            .AsNoTracking()
            .Where(s => s.EndedAt == null)
            .OrderBy(s => s.Id)
            .Select(s => s.SpotId)
            .ToListAsync();

        foreach (var spotId in running)
        {
            await RestoreOnSpotAsync(spotId);
        }
    }

    public async Task EndRunningInsideGateAsync(int spotId, SessionEndReason reason, AuditActor actor)
    {
        var session = await RunningSessionAsync(spotId);
        if (session is null)
        {
            return;
        }

        await EndInsideGateAsync(session, reason, actor);
    }

    public async Task EndInsideGateAsync(Session session, SessionEndReason reason, AuditActor actor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        session.End(now, reason, actor.UserId, actor.Name);

        var target = AuditTarget.Spot(session.SpotId);
        if (reason == SessionEndReason.Stopped)
        {
            audit.Record(actor, AuditActions.SessionStopped, target);
        }
        else
        {
            var details = reason == SessionEndReason.TimeUp ? TimeUpReason : ManualOffReason;
            audit.Record(actor, AuditActions.SessionEnded, target, details);
        }

        await db.SaveChangesAsync();

        IVenueEvent ended = reason == SessionEndReason.Stopped
            ? new SessionStopped(session.SpotId, session.Id)
            : new SessionEnded(session.SpotId, session.Id);
        events.Publish(ended);
    }


    private async Task EndDueOnSpotAsync(int spotId, AuditActor actor)
    {
        var mapped = await MappedSpotAsync(spotId);
        if (mapped is null)
        {
            return;
        }

        var (device, channel) = mapped.Value;
        await gate.RunAsync(device, board => EndDueInsideGateAsync(board, spotId, channel, actor));
    }

    private async Task<Result<bool>> EndDueInsideGateAsync(Board board, int spotId, int channel, AuditActor actor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = await RunningSessionAsync(spotId);
        if (session is null || session.EndsAt > now)
        {
            return Result<bool>.Ok(false);
        }

        var error = await SwitchOffAsync(board, channel);
        if (error is not null)
        {
            error = await SwitchOffAsync(board, channel);
        }

        await EndInsideGateAsync(session, SessionEndReason.TimeUp, actor);
        if (error is not null)
        {
            await RecordSwitchOffFailedAsync(session, actor, error);
        }

        return Result<bool>.Ok(true);
    }

    private static async Task<string?> SwitchOffAsync(Board board, int channel)
    {
        var switched = await board.SwitchAsync(channel, false);
        if (switched.IsFailure)
        {
            return switched.Error;
        }

        if (switched.Value)
        {
            return SpotError.StateMismatch;
        }

        return null;
    }

    private async Task RecordSwitchOffFailedAsync(Session session, AuditActor actor, string error)
    {
        warnings.Add(session.SpotId);
        var target = AuditTarget.Spot(session.SpotId);
        var reason = SwitchFailures.ReasonFor(error);
        audit.Record(actor, AuditActions.SessionSwitchOffFailed, target, reason);
        await db.SaveChangesAsync();
        events.Publish(new SessionSwitchOffFailed(session.SpotId, session.Id));
    }

    private async Task RestoreOnSpotAsync(int spotId)
    {
        var mapped = await MappedSpotAsync(spotId);
        if (mapped is null)
        {
            return;
        }

        var (device, channel) = mapped.Value;
        var status = statuses.Get(device.Id);
        if (status.IsOn(channel) != false)
        {
            return;
        }

        await gate.RunAsync(device, board => RestoreInsideGateAsync(board, spotId, device.Id, channel));
    }

    private async Task<Result<bool>> RestoreInsideGateAsync(Board board, int spotId, int deviceId, int channel)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = await RunningSessionAsync(spotId);
        if (session is null || session.EndsAt <= now)
        {
            return Result<bool>.Ok(false);
        }

        var status = statuses.Get(deviceId);
        if (status.IsOn(channel) != false)
        {
            return Result<bool>.Ok(false);
        }

        var switched = await board.SwitchAsync(channel, true);
        if (switched.IsFailure || !switched.Value)
        {
            return Result<bool>.Ok(false);
        }

        var target = AuditTarget.Spot(spotId);
        audit.Record(AuditActor.DriftFix, AuditActions.SessionRestored, target);
        await db.SaveChangesAsync();
        events.Publish(new SessionRestored(spotId, session.Id));
        return Result<bool>.Ok(true);
    }

    private async Task<(Device Device, int Channel)?> MappedSpotAsync(int spotId)
    {
        var spot = await db.Spots.AsNoTracking().SingleAsync(s => s.Id == spotId);
        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return null;
        }

        var device = await db.Devices.AsNoTracking().SingleAsync(d => d.Id == deviceId);
        return (device, channel);
    }

    private async Task<Result<Session>> StartInsideGateAsync(Board board, PreparedCharge charge, AuditActor actor)
    {
        var spotId = charge.Target.Spot.Id;
        var running = await RunningSessionAsync(spotId);
        if (running is not null)
        {
            return Fail(SessionError.AlreadyRunning);
        }

        var switched = await board.SwitchAsync(charge.Target.Channel, true);
        if (switched.IsFailure)
        {
            await failures.RecordAsync(spotId, actor, switched.Error);
            return Fail(switched.Error);
        }

        if (!switched.Value)
        {
            await failures.RecordAsync(spotId, actor, SpotError.StateMismatch);
            return Fail(SpotError.StateMismatch);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var endsAt = now.AddMinutes(charge.Minutes);
        var session = new Session(spotId, now, endsAt, actor.UserId, actor.Name);

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        RecordCharge(session, ChargeKind.Start, charge, actor, now, AuditActions.SessionStarted);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result<Session>.Ok(session);
    }

    private async Task<Result<Session>> ExtendInsideGateAsync(PreparedCharge charge, AuditActor actor)
    {
        var session = await RunningSessionAsync(charge.Target.Spot.Id);
        if (session is null)
        {
            return Fail(SessionError.NotRunning);
        }

        var now = time.GetUtcNow().UtcDateTime;
        session.Extend(charge.Minutes);
        RecordCharge(session, ChargeKind.Extend, charge, actor, now, AuditActions.SessionExtended);
        await db.SaveChangesAsync();

        return Result<Session>.Ok(session);
    }

    private async Task<Result<Session>> StopInsideGateAsync(Board board, SpotTarget target, AuditActor actor)
    {
        var spotId = target.Spot.Id;
        var session = await RunningSessionAsync(spotId);
        if (session is null)
        {
            return Fail(SessionError.NotRunning);
        }

        var switched = await board.SwitchAsync(target.Channel, false);
        if (switched.IsFailure)
        {
            await failures.RecordAsync(spotId, actor, switched.Error);
            return Fail(switched.Error);
        }

        if (switched.Value)
        {
            await failures.RecordAsync(spotId, actor, SpotError.StateMismatch);
            return Fail(SpotError.StateMismatch);
        }

        await EndInsideGateAsync(session, SessionEndReason.Stopped, actor);
        return Result<Session>.Ok(session);
    }

    private void RecordCharge(Session session, ChargeKind kind, PreparedCharge charge, AuditActor actor, DateTime now, string action)
    {
        var sessionCharge = new SessionCharge(
            session.Id,
            kind,
            now,
            actor.UserId,
            actor.Name,
            charge.Minutes,
            charge.ComputedTetri,
            charge.ChargedTetri);
        db.SessionCharges.Add(sessionCharge);

        var target = AuditTarget.Spot(session.SpotId);
        var details = ChargeDetails(charge);
        audit.Record(actor, action, target, details);
    }

    private async Task<Result<PreparedCharge>> PrepareChargeAsync(int spotId, int minutes, decimal? amount, AuditActor actor)
    {
        if (!AreValid(minutes))
        {
            return Result<PreparedCharge>.Fail(SessionError.InvalidMinutes);
        }

        var found = await FindTargetAsync(spotId, actor);
        if (found.IsFailure)
        {
            return Result<PreparedCharge>.Fail(found.Error);
        }

        var target = found.Value;
        var computed = Money.PriceTetri(minutes, target.HourlyRateTetri);
        var charged = ChargedTetri(amount, computed);
        if (charged is not long chargedTetri)
        {
            return Result<PreparedCharge>.Fail(SessionError.InvalidAmount);
        }

        var prepared = new PreparedCharge(target, minutes, computed, chargedTetri);
        return Result<PreparedCharge>.Ok(prepared);
    }

    private async Task<Result<SpotTarget>> FindTargetAsync(int spotId, AuditActor actor)
    {
        var spot = await visibleSpots.FindAsync(spotId, actor);
        if (spot is null)
        {
            return Result<SpotTarget>.Fail(SpotError.NotFound);
        }

        if (spot.IsArchived)
        {
            return Result<SpotTarget>.Fail(SpotError.Archived);
        }

        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return Result<SpotTarget>.Fail(SpotError.Unmapped);
        }

        var device = await db.Devices.SingleAsync(d => d.Id == deviceId);
        var group = await db.Groups.SingleAsync(g => g.Id == spot.GroupId);
        var target = new SpotTarget(spot, device, channel, group.HourlyRateTetri);
        return Result<SpotTarget>.Ok(target);
    }

    private Task<Session?> RunningSessionAsync(int spotId)
    {
        return db.Sessions.SingleOrDefaultAsync(s => s.SpotId == spotId && s.EndedAt == null);
    }

    private static bool AreValid(int minutes)
    {
        return minutes >= MinMinutes && minutes <= MaxMinutes;
    }

    private static long? ChargedTetri(decimal? amount, long computedTetri)
    {
        if (amount is not decimal typed)
        {
            return computedTetri;
        }

        var computedGel = Money.ToGel(computedTetri);
        if (typed < 0 || typed > computedGel)
        {
            return null;
        }

        if (!Money.HasAtMostTwoDecimals(typed))
        {
            return null;
        }

        return Money.ToTetri(typed);
    }

    private static string ChargeDetails(PreparedCharge charge)
    {
        var computed = Money.Show(charge.ComputedTetri);
        if (charge.ChargedTetri == charge.ComputedTetri)
        {
            return $"{charge.Minutes} min, {computed}";
        }

        var charged = Money.Show(charge.ChargedTetri);
        return $"{charge.Minutes} min, {computed} → {charged}";
    }

    private static Result<Session> Fail(string error)
    {
        return Result<Session>.Fail(error);
    }

    private record SpotTarget(Spot Spot, Device Device, int Channel, long HourlyRateTetri);

    private record PreparedCharge(SpotTarget Target, int Minutes, long ComputedTetri, long ChargedTetri);
}
