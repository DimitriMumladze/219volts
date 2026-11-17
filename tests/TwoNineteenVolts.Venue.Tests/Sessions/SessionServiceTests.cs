using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Events;
using TwoNineteenVolts.Venue.Tests.Groups;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Sessions;

public class SessionServiceTests
{
    private const int Channel = 3;

    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);

    [Fact]
    public async Task Start_switches_on_charges_and_audits()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        PublishedEvents.Take(factory);

        var result = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));

        var session = await SingleSessionAsync(factory);
        var charge = await SingleChargeAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionStarted);
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        Assert.False(result.IsFailure);
        Assert.Equal(spot, session.SpotId);
        var endsAt = now.AddMinutes(60);
        Assert.Equal(now, session.StartedAt);
        Assert.Equal(endsAt, session.EndsAt);
        Assert.True(session.IsRunning);
        Assert.Equal("Gio", session.StartedByName);
        Assert.Equal(ChargeKind.Start, charge.Kind);
        Assert.Equal(60, charge.Minutes);
        Assert.Equal(1000, charge.ComputedTetri);
        Assert.Equal(1000, charge.ChargedTetri);
        Assert.Contains($"set {panel}/{Channel} on", factory.Waveshare.Calls);
        Assert.Equal($"spot:{spot}", entry.Target);
        Assert.Equal("60 min, 10.00", entry.Details);
        Assert.Equal([new SessionStarted(spot, session.Id)], spotEvents);
    }

    [Fact]
    public async Task Start_with_lower_amount_stores_both()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);

        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, 5m));

        var charge = await SingleChargeAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionStarted);
        Assert.Equal(1000, charge.ComputedTetri);
        Assert.Equal(500, charge.ChargedTetri);
        Assert.Equal("60 min, 10.00 → 5.00", entry.Details);
    }

    [Fact]
    public async Task Start_on_a_busy_spot_is_refused()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));

        var second = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 30, null));

        var count = await CountAsync(factory);
        Assert.Equal(SessionError.AlreadyRunning, second.Error);
        Assert.Equal((1, 1), count);
    }

    [Theory]
    [InlineData(DeviceError.NoAnswer, "no answer")]
    [InlineData(DeviceError.ErrorAnswer, "error answer")]
    public async Task Failed_start_creates_no_session_and_no_charge(string error, string reason)
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        factory.Waveshare.Error = error;

        var result = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));

        var count = await CountAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SpotSwitchFailed);
        Assert.Equal(error, result.Error);
        Assert.Equal((0, 0), count);
        Assert.Equal(reason, entry.Details);
    }

    [Fact]
    public async Task Start_that_reads_off_creates_no_session()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        factory.Waveshare.ShouldIgnoreSet = true;

        var result = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));

        var count = await CountAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SpotSwitchFailed);
        Assert.Equal(DeviceError.DidNotObey, result.Error);
        Assert.Equal((0, 0), count);
        Assert.Equal("mismatch", entry.Details);
    }

    [Fact]
    public async Task Extend_adds_minutes_and_a_charge_at_the_current_rate()
    {
        using var factory = new VenueAppFactory();
        var (_, spot, group) = await AddSpotWithGroupAsync(factory);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        await SetRateAsync(factory, group, 1200);
        PublishedEvents.Take(factory);

        var result = await UseServiceAsync(factory, sessions => sessions.ExtendAsync(spot, 30, null));

        var session = await SingleSessionAsync(factory);
        var charges = await ChargesAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionExtended);
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        var kinds = charges.Select(c => c.Kind).ToList();
        var endsAt = now.AddMinutes(90);
        Assert.False(result.IsFailure);
        Assert.Equal(endsAt, session.EndsAt);
        Assert.Equal([ChargeKind.Start, ChargeKind.Extend], kinds);
        Assert.Equal(600, charges[1].ComputedTetri);
        Assert.Equal("30 min, 6.00", entry.Details);
        Assert.Equal([new SessionExtended(spot, session.Id)], spotEvents);
    }

    [Fact]
    public async Task Extend_does_not_talk_to_the_board()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        var callsBefore = factory.Waveshare.Calls.Count;

        await UseServiceAsync(factory, sessions => sessions.ExtendAsync(spot, 30, null));

        Assert.Equal(callsBefore, factory.Waveshare.Calls.Count);
    }

    [Fact]
    public async Task Extend_or_stop_without_a_session_is_refused()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);

        var extend = await UseServiceAsync(factory, sessions => sessions.ExtendAsync(spot, 30, null));
        var stop = await UseServiceAsync(factory, sessions => sessions.StopAsync(spot));

        Assert.Equal(SessionError.NotRunning, extend.Error);
        Assert.Equal(SessionError.NotRunning, stop.Error);
    }

    [Fact]
    public async Task Stop_switches_off_and_keeps_the_amount()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        factory.Time.Advance(TimeSpan.FromMinutes(20));
        var now = factory.Time.GetUtcNow().UtcDateTime;
        PublishedEvents.Take(factory);

        var result = await UseServiceAsync(factory, sessions => sessions.StopAsync(spot));

        var session = await SingleSessionAsync(factory);
        var charge = await SingleChargeAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionStopped);
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        Assert.False(result.IsFailure);
        Assert.False(session.IsRunning);
        Assert.Equal(now, session.EndedAt);
        Assert.Equal(SessionEndReason.Stopped, session.EndReason);
        Assert.Equal("Gio", session.EndedByName);
        Assert.Equal(1000, charge.ChargedTetri);
        Assert.Contains($"set {panel}/{Channel} off", factory.Waveshare.Calls);
        Assert.Equal($"spot:{spot}", entry.Target);
        Assert.Equal([new SessionStopped(spot, session.Id)], spotEvents);
    }

    [Fact]
    public async Task Failed_stop_keeps_the_session_running()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        factory.Waveshare.Error = DeviceError.NoAnswer;

        var result = await UseServiceAsync(factory, sessions => sessions.StopAsync(spot));

        var session = await SingleSessionAsync(factory);
        Assert.Equal(DeviceError.NoAnswer, result.Error);
        Assert.True(session.IsRunning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(601)]
    public async Task Minutes_outside_1_to_600_are_refused(int minutes)
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);

        var start = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, minutes, null));
        var price = await UsePriceAsync(factory, spot, minutes);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        var extend = await UseServiceAsync(factory, sessions => sessions.ExtendAsync(spot, minutes, null));

        Assert.Equal(SessionError.InvalidMinutes, start.Error);
        Assert.Equal(SessionError.InvalidMinutes, price.Error);
        Assert.Equal(SessionError.InvalidMinutes, extend.Error);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.005")]
    [InlineData("10.01")]
    public async Task Odd_amounts_are_refused(string amountText)
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        var amount = decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture);

        var result = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, amount));

        var count = await CountAsync(factory);
        Assert.Equal(SessionError.InvalidAmount, result.Error);
        Assert.Equal((0, 0), count);
    }

    [Fact]
    public async Task Free_time_is_allowed()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);

        var result = await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, 0m));

        var charge = await SingleChargeAsync(factory);
        Assert.False(result.IsFailure);
        Assert.Equal(0, charge.ChargedTetri);
    }

    [Fact]
    public async Task Two_starts_at_once_give_one_session()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        factory.Waveshare.SetGate = new TaskCompletionSource();

        var first = UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Count > 0, TimeSpan.FromSeconds(5));
        var second = UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 30, null));
        factory.Waveshare.SetGate.SetResult();
        var results = await Task.WhenAll(first, second);

        var errors = results.Select(r => r.Error).ToList();
        var count = await CountAsync(factory);
        Assert.Equal([null, SessionError.AlreadyRunning], errors);
        Assert.Equal((1, 1), count);
    }

    [Fact]
    public async Task Giving_up_at_the_door_on_start_and_stop_is_audited()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        var (_, other) = await AddSecondSpotAsync(factory, panel);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(other, 60, null));
        var device = await FindDeviceAsync(factory, panel);
        factory.Waveshare.Gate = new TaskCompletionSource();
        var check = ActivatorUtilities.CreateInstance<DeviceCheckService>(factory.Services).RunOnceAsync();
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"read-all {device.Id}"), TimeSpan.FromSeconds(5));

        var start = await GiveUpAtTheDoorAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        var stop = await GiveUpAtTheDoorAsync(factory, sessions => sessions.StopAsync(other));
        factory.Waveshare.Gate.SetResult();
        await check;

        var details = await AuditDetailsAsync(factory, AuditActions.SpotSwitchFailed);
        Assert.Equal(DeviceError.NoAnswer, start.Error);
        Assert.Equal(DeviceError.NoAnswer, stop.Error);
        Assert.Equal(["no answer", "no answer"], details);
    }

    [Fact]
    public async Task Price_preview_matches_the_charge()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);

        var price = await UsePriceAsync(factory, spot, 7);
        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 7, null));

        var charge = await SingleChargeAsync(factory);
        Assert.Equal(117, price.Value);
        Assert.Equal(117, charge.ChargedTetri);
    }

    [Fact]
    public async Task Start_clears_a_switch_off_warning()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        warnings.Add(spot);

        await UseServiceAsync(factory, sessions => sessions.StartAsync(spot, 60, null));

        Assert.False(warnings.HasWarning(spot));
    }

    [Fact]
    public async Task Unknown_spot_is_not_found()
    {
        using var factory = new VenueAppFactory();

        var start = await UseServiceAsync(factory, sessions => sessions.StartAsync(99, 60, null));
        var price = await UsePriceAsync(factory, 99, 60);

        Assert.Equal(SpotError.NotFound, start.Error);
        Assert.Equal(SpotError.NotFound, price.Error);
    }


    private static async Task<(int Panel, int Spot)> AddSecondSpotAsync(VenueAppFactory factory, int panel)
    {
        var group = await TestGroups.AddAsync(factory, "Pool", 1000);
        var spot = await TestSpots.AddAsync(factory, "Pool 1", panel, Channel + 1, groupId: group);
        return (panel, spot);
    }

    private static async Task<Result<Session>> GiveUpAtTheDoorAsync(VenueAppFactory factory, Func<SessionService, Task<Result<Session>>> action)
    {
        var running = UseServiceAsync(factory, action);
        SpinWait.SpinUntil(
            () =>
            {
                factory.Time.Advance(TimeSpan.FromSeconds(2));
                return running.IsCompleted;
            },
            TimeSpan.FromSeconds(5));
        return await running;
    }

    private static async Task<Device> FindDeviceAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Devices.SingleAsync(d => d.Id == id);
    }

    private static async Task<List<string?>> AuditDetailsAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries
            .Where(e => e.Action == action)
            .OrderBy(e => e.Id)
            .Select(e => e.Details)
            .ToListAsync();
    }

    private static async Task<(int Panel, int Spot)> AddSpotAsync(VenueAppFactory factory)
    {
        var (panel, spot, _) = await AddSpotWithGroupAsync(factory);
        return (panel, spot);
    }

    private static async Task<(int Panel, int Spot, int Group)> AddSpotWithGroupAsync(VenueAppFactory factory)
    {
        var panel = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
        var group = await TestGroups.AddAsync(factory, "Billiards", 1000);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, Channel, groupId: group);
        return (panel, spot, group);
    }

    private static async Task<Result<Session>> UseServiceAsync(VenueAppFactory factory, Func<SessionService, Task<Result<Session>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var service = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        return await action(service);
    }

    private static async Task<Result<long>> UsePriceAsync(VenueAppFactory factory, int spot, int minutes)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var service = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        return await service.PriceAsync(spot, minutes);
    }

    private static async Task SetRateAsync(VenueAppFactory factory, int groupId, long rateTetri)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var group = await db.Groups.SingleAsync(g => g.Id == groupId);
        group.SetRate(rateTetri);
        await db.SaveChangesAsync();
    }

    private static async Task<Session> SingleSessionAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Sessions.SingleAsync();
    }

    private static async Task<SessionCharge> SingleChargeAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.SessionCharges.SingleAsync();
    }

    private static async Task<List<SessionCharge>> ChargesAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.SessionCharges.OrderBy(c => c.Id).ToListAsync();
    }

    private static async Task<(int Sessions, int Charges)> CountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var sessions = await db.Sessions.CountAsync();
        var charges = await db.SessionCharges.CountAsync();
        return (sessions, charges);
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }
}
