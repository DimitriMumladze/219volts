using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Application.Sessions;

public class SessionService(
    IVenueDbContext db,
    SpotReader reader,
    IDeviceGateway devices,
    SwitchFailures failures,
    SwitchOffWarnings warnings,
    IAuditLog audit,
    ICurrentActor currentActor,
    IEventQueue events,
    TimeProvider time,
    ILogger<SessionService> logger)
{
    public const int MinMinutes = 1;
    public const int MaxMinutes = 600;

    private const string TimeUpReason = "time up";
    private const string ManualOffReason = "manual off";

    public async Task<Result<long>> PriceAsync(int spotId, int minutes)
    {
        if (!AreMinutesInRange(minutes))
        {
            return Result<long>.Fail(SessionError.InvalidMinutes);
        }

        var actor = currentActor.Get();
        var found = await FindTargetAsync(spotId, actor);
        if (found.IsFailure)
        {
            return Result<long>.Fail(found.Error);
        }

        var price = SessionPricing.PriceTetri(minutes, found.Value.HourlyRateTetri);
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
        var hasEnteredHold = false;
        var started = await devices.WithOutletAsync(charge.Target.Outlet, control =>
        {
            hasEnteredHold = true;
            return StartInsideHoldAsync(control, charge, actor);
        });
        if (started.IsFailure)
        {
            if (hasEnteredHold)
            {
                return started;
            }

            return await FailOutsideHoldAsync(spotId, actor, started.Error);
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
        var extended = await devices.WithOutletAsync(charge.Target.Outlet, _ => ExtendInsideHoldAsync(charge, actor));
        if (extended.IsFailure)
        {
            var spotError = SwitchFailures.ToSpotError(extended.Error);
            return Fail(spotError);
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
        var hasEnteredHold = false;
        var stopped = await devices.WithOutletAsync(target.Outlet, control =>
        {
            hasEnteredHold = true;
            return StopInsideHoldAsync(control, target, actor);
        });
        if (stopped.IsFailure && !hasEnteredHold)
        {
            return await FailOutsideHoldAsync(spotId, actor, stopped.Error);
        }

        return stopped;
    }

    public async Task<List<int>> FindDeviceIdsWithDueSessionsAsync()
    {
        var now = time.GetUtcNow().UtcDateTime;
        var deviceIds = await db.Sessions
            .AsNoTracking()
            .Where(s => s.EndedAt == null && s.EndsAt <= now)
            .Join(db.Spots, s => s.SpotId, spot => spot.Id, (s, spot) => spot.DeviceId)
            .Distinct()
            .ToListAsync();

        return deviceIds.OfType<int>().ToList();
    }

    public async Task EndDueOnDeviceAsync(int deviceId, AuditActor actor, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var due = await db.Sessions
            .AsNoTracking()
            .Where(s => s.EndedAt == null && s.EndsAt <= now)
            .Join(db.Spots, s => s.SpotId, spot => spot.Id, (s, spot) => new { s.EndsAt, SpotId = spot.Id, spot.DeviceId, spot.Channel })
            .Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.EndsAt)
            .ToListAsync();

        foreach (var item in due)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (item.Channel is not int channel)
            {
                continue;
            }

            await devices.WithOutletAsync(new Outlet(deviceId, channel), control => EndDueInsideHoldAsync(control, item.SpotId, actor));
        }
    }

    public async Task RestoreDriftedAsync(CancellationToken cancellationToken)
    {
        var running = await db.Sessions
            .AsNoTracking()
            .Where(s => s.EndedAt == null)
            .Join(db.Spots, s => s.SpotId, spot => spot.Id, (s, spot) => new { SpotId = spot.Id, spot.DeviceId, spot.Channel })
            .OrderBy(x => x.SpotId)
            .ToListAsync();
        var outlets = running
            .Where(x => x.DeviceId != null && x.Channel != null)
            .Select(x => new { x.SpotId, Outlet = new Outlet(x.DeviceId!.Value, x.Channel!.Value) })
            .ToList();
        var outletList = outlets.Select(x => x.Outlet).ToList();
        var states = await devices.StatesOfAsync(outletList);

        foreach (var item in outlets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!states.TryGetValue(item.Outlet, out var state))
            {
                continue;
            }

            var isKnownOff = state.IsOn == false;
            if (!isKnownOff)
            {
                continue;
            }

            await devices.WithOutletAsync(item.Outlet, control => RestoreInsideHoldAsync(control, item.SpotId));
        }
    }

    public Task<Result> SwitchOffAndEndAsync(Outlet outlet, int spotId, AuditActor actor)
    {
        return devices.WithOutletAsync(outlet, control => SwitchOffAndEndInsideHoldAsync(control, spotId, actor));
    }


    private async Task<Result> SwitchOffAndEndInsideHoldAsync(IOutletControl control, int spotId, AuditActor actor)
    {
        var switchOff = await control.SwitchAsync(shouldBeOn: false);
        if (switchOff.IsFailure)
        {
            return switchOff;
        }

        var session = await RunningSessionAsync(spotId);
        if (session is not null)
        {
            await EndInsideHoldAsync(session, SessionEndReason.ManualOff, actor);
        }

        return Result.Ok();
    }

    private async Task EndInsideHoldAsync(Session session, SessionEndReason reason, AuditActor actor)
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

    private async Task<Result> EndDueInsideHoldAsync(IOutletControl control, int spotId, AuditActor actor)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = await RunningSessionAsync(spotId);
        if (session is null || session.EndsAt > now)
        {
            return Result.Ok();
        }

        var switchOff = await SwitchOffWithOneRetryAsync(control);
        await EndInsideHoldAsync(session, SessionEndReason.TimeUp, actor);
        if (switchOff.IsFailure)
        {
            await RecordSwitchOffFailedAsync(session, actor, switchOff.Error);
        }

        return Result.Ok();
    }

    private static async Task<Result> SwitchOffWithOneRetryAsync(IOutletControl control)
    {
        var firstTry = await control.SwitchAsync(shouldBeOn: false);
        if (firstTry.IsFailure)
        {
            var secondTry = await control.SwitchAsync(shouldBeOn: false);
            return secondTry;
        }

        return firstTry;
    }

    private async Task RecordSwitchOffFailedAsync(Session session, AuditActor actor, string deviceError)
    {
        failures.Log(session.SpotId, deviceError);
        warnings.Add(session.SpotId);
        var target = AuditTarget.Spot(session.SpotId);
        var reason = SwitchFailures.AuditReasonFor(deviceError);
        audit.Record(actor, AuditActions.SessionSwitchOffFailed, target, reason);
        await db.SaveChangesAsync();
        events.Publish(new SessionSwitchOffFailed(session.SpotId, session.Id));
    }

    private async Task<Result> RestoreInsideHoldAsync(IOutletControl control, int spotId)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = await RunningSessionAsync(spotId);
        if (session is null || session.EndsAt <= now)
        {
            return Result.Ok();
        }

        var isKnownOff = control.State.IsOn == false;
        if (!isKnownOff)
        {
            return Result.Ok();
        }

        var switchOn = await control.SwitchAsync(shouldBeOn: true);
        if (switchOn.IsFailure)
        {
            if (switchOn.Error == DeviceError.DidNotObey)
            {
                control.ForgetState();
            }

            logger.LogWarning("Drift fix for spot {SpotId} failed: {Error}", spotId, switchOn.Error);
            return switchOn;
        }

        var target = AuditTarget.Spot(spotId);
        audit.Record(AuditActor.DriftFix, AuditActions.SessionRestored, target);
        await db.SaveChangesAsync();
        events.Publish(new SessionRestored(spotId, session.Id));
        return Result.Ok();
    }

    private async Task<Result<Session>> StartInsideHoldAsync(IOutletControl control, PreparedCharge charge, AuditActor actor)
    {
        var spotId = charge.Target.Spot.Id;
        var running = await RunningSessionAsync(spotId);
        if (running is not null)
        {
            return Fail(SessionError.AlreadyRunning);
        }

        var switchOn = await control.SwitchAsync(shouldBeOn: true);
        if (switchOn.IsFailure)
        {
            await failures.RecordAsync(spotId, actor, switchOn.Error);
            return Fail(switchOn.Error);
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

    private async Task<Result<Session>> ExtendInsideHoldAsync(PreparedCharge charge, AuditActor actor)
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

    private async Task<Result<Session>> StopInsideHoldAsync(IOutletControl control, SpotTarget target, AuditActor actor)
    {
        var spotId = target.Spot.Id;
        var session = await RunningSessionAsync(spotId);
        if (session is null)
        {
            return Fail(SessionError.NotRunning);
        }

        var switchOff = await control.SwitchAsync(shouldBeOn: false);
        if (switchOff.IsFailure)
        {
            await failures.RecordAsync(spotId, actor, switchOff.Error);
            return Fail(switchOff.Error);
        }

        await EndInsideHoldAsync(session, SessionEndReason.Stopped, actor);
        return Result<Session>.Ok(session);
    }

    private void RecordCharge(Session session, ChargeKind kind, PreparedCharge charge, AuditActor actor, DateTime now, string action)
    {
        var sessionCharge = new SessionCharge
        {
            SessionId = session.Id,
            Kind = kind,
            At = now,
            UserId = actor.UserId,
            UserName = actor.Name,
            Minutes = charge.Minutes,
            ComputedTetri = charge.ComputedTetri,
            ChargedTetri = charge.ChargedTetri
        };
        db.SessionCharges.Add(sessionCharge);

        var target = AuditTarget.Spot(session.SpotId);
        var details = SessionPricing.Details(charge.Minutes, charge.ComputedTetri, charge.ChargedTetri);
        audit.Record(actor, action, target, details);
    }

    private async Task<Result<PreparedCharge>> PrepareChargeAsync(int spotId, int minutes, decimal? amount, AuditActor actor)
    {
        if (!AreMinutesInRange(minutes))
        {
            return Result<PreparedCharge>.Fail(SessionError.InvalidMinutes);
        }

        var found = await FindTargetAsync(spotId, actor);
        if (found.IsFailure)
        {
            return Result<PreparedCharge>.Fail(found.Error);
        }

        var target = found.Value;
        var computedTetri = SessionPricing.PriceTetri(minutes, target.HourlyRateTetri);
        var charged = SessionPricing.ChargedTetri(amount, computedTetri);
        if (charged.IsFailure)
        {
            return Result<PreparedCharge>.Fail(charged.Error);
        }

        var prepared = new PreparedCharge(target, minutes, computedTetri, charged.Value);
        return Result<PreparedCharge>.Ok(prepared);
    }

    private async Task<Result<SpotTarget>> FindTargetAsync(int spotId, AuditActor actor)
    {
        var spot = await reader.FindVisibleAsync(spotId, actor);
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

        var group = await db.Groups.SingleAsync(g => g.Id == spot.GroupId);
        var outlet = new Outlet(deviceId, channel);
        var target = new SpotTarget(spot, outlet, group.HourlyRateTetri);
        return Result<SpotTarget>.Ok(target);
    }

    private async Task<Result<Session>> FailOutsideHoldAsync(int spotId, AuditActor actor, string deviceError)
    {
        await failures.RecordAsync(spotId, actor, deviceError);
        var spotError = SwitchFailures.ToSpotError(deviceError);
        return Fail(spotError);
    }

    private Task<Session?> RunningSessionAsync(int spotId)
    {
        return db.Sessions.SingleOrDefaultAsync(s => s.SpotId == spotId && s.EndedAt == null);
    }

    private static bool AreMinutesInRange(int minutes)
    {
        return minutes >= MinMinutes && minutes <= MaxMinutes;
    }

    private static Result<Session> Fail(string error)
    {
        return Result<Session>.Fail(error);
    }

    private record SpotTarget(Spot Spot, Outlet Outlet, long HourlyRateTetri);

    private record PreparedCharge(SpotTarget Target, int Minutes, long ComputedTetri, long ChargedTetri);
}
