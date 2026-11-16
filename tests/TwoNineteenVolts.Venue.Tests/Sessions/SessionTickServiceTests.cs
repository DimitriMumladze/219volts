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
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Events;
using TwoNineteenVolts.Venue.Infrastructure.Sessions;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Events;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Sessions;

public class SessionTickServiceTests
{
    private const int Channel = 3;

    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);
    private static readonly TimeSpan OneHour = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Due_session_ends_and_switches_off()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(OneHour);
        PublishedEvents.Take(factory);

        await TickAsync(factory);

        var session = await SingleSessionAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionEnded);
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        Assert.False(session.IsRunning);
        Assert.Equal(SessionEndReason.TimeUp, session.EndReason);
        Assert.Equal("Timer", session.EndedByName);
        Assert.Contains($"set {panel}/{Channel} off", factory.Waveshare.Calls);
        Assert.Equal("Timer", entry.ActorName);
        Assert.Equal("time up", entry.Details);
        Assert.Equal([new SessionEnded(spot, session.Id)], spotEvents);
    }

    [Fact]
    public async Task Not_yet_due_session_keeps_running()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        var almostAnHour = OneHour - TimeSpan.FromSeconds(1);
        factory.Time.Advance(almostAnHour);

        await TickAsync(factory);

        var session = await SingleSessionAsync(factory);
        Assert.True(session.IsRunning);
        Assert.DoesNotContain($"set {panel}/{Channel} off", factory.Waveshare.Calls);
    }

    [Fact]
    public async Task Failed_off_retries_once_then_ends_with_a_warning()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(OneHour);
        factory.Waveshare.Error = DeviceError.ErrorAnswer;
        PublishedEvents.Take(factory);

        await TickAsync(factory);

        var session = await SingleSessionAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionSwitchOffFailed);
        var offAttempts = factory.Waveshare.Calls.Count(c => c == $"set {panel}/{Channel} off");
        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        Assert.False(session.IsRunning);
        Assert.Equal(2, offAttempts);
        Assert.True(warnings.Has(spot));
        Assert.Equal("error answer", entry.Details);
        Assert.Contains(new SessionSwitchOffFailed(spot, session.Id), spotEvents);
    }

    [Fact]
    public async Task Warning_clears_when_the_relay_reads_off()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(OneHour);
        factory.Waveshare.Error = DeviceError.ErrorAnswer;
        await TickAsync(factory);
        factory.Waveshare.Error = null;
        var device = await FindDeviceAsync(factory, panel);
        var allOff = new bool[16];
        factory.Waveshare.SetChannels(device, allOff);

        await CheckAsync(factory);
        await DeliverAsync(factory);

        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        Assert.False(warnings.Has(spot));
    }

    [Fact]
    public async Task Startup_ends_overdue_sessions_as_restart()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(OneHour * 2);

        await StartupAsync(factory);

        var session = await SingleSessionAsync(factory);
        Assert.False(session.IsRunning);
        Assert.Equal("Restart", session.EndedByName);
    }

    [Fact]
    public async Task Running_session_reading_off_is_switched_back_on()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        await TurnOffBehindTheBackAsync(factory, panel);
        PublishedEvents.Take(factory);
        var callsBefore = factory.Waveshare.Calls.Count;

        await TickAsync(factory);

        var newCalls = factory.Waveshare.Calls.Skip(callsBefore).ToList();
        var entry = await SingleAuditAsync(factory, AuditActions.SessionRestored);
        var state = Statuses(factory).Get(panel).IsOn(Channel);
        var session = await SingleSessionAsync(factory);
        var spotEvents = PublishedEvents.Take(factory).OfType<ISpotEvent>().ToList();
        Assert.Contains($"set {panel}/{Channel} on", newCalls);
        Assert.True(state);
        Assert.Equal("Drift fix", entry.ActorName);
        Assert.Contains(new SessionRestored(spot, session.Id), spotEvents);
    }

    [Fact]
    public async Task Drift_fix_does_nothing_after_a_stop()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        await TurnOffBehindTheBackAsync(factory, panel);
        factory.Waveshare.SetGate = new TaskCompletionSource();

        var stopping = UseSessionsAsync(factory, sessions => sessions.StopAsync(spot));
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"set {panel}/{Channel} off"), FiveSeconds);
        var ticking = TickAsync(factory);
        factory.Waveshare.SetGate.SetResult();
        var stopped = await stopping;
        await ticking;

        var session = await SingleSessionAsync(factory);
        var restored = await CountAuditAsync(factory, AuditActions.SessionRestored);
        var callsAfterStart = factory.Waveshare.Calls.Skip(1).ToList();
        Assert.False(stopped.IsFailure);
        Assert.False(session.IsRunning);
        Assert.DoesNotContain($"set {panel}/{Channel} on", callsAfterStart);
        Assert.Equal(0, restored);
    }

    [Fact]
    public async Task Spot_on_without_session_is_left_alone()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await UseSpotsAsync(factory, spots => spots.SwitchAsync(spot, true));

        await TickAsync(factory);

        Assert.DoesNotContain($"set {panel}/{Channel} off", factory.Waveshare.Calls);
    }

    [Fact]
    public async Task Due_sessions_on_a_dead_board_all_end_with_warnings()
    {
        using var factory = new VenueAppFactory();
        var (panel, first) = await AddSpotAsync(factory);
        var second = await TestSpots.AddAsync(factory, "Billiard 2", panel, 4);
        await StartAsync(factory, first);
        await StartAsync(factory, second);
        factory.Time.Advance(OneHour);
        factory.Waveshare.Error = DeviceError.NoAnswer;

        await TickAsync(factory);

        var running = await RunningCountAsync(factory);
        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        Assert.Equal(0, running);
        Assert.True(warnings.Has(first));
        Assert.True(warnings.Has(second));
    }

    [Fact]
    public async Task Extend_after_the_tick_ended_it_is_refused()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(OneHour);
        await TickAsync(factory);

        var extend = await UseSessionsAsync(factory, sessions => sessions.ExtendAsync(spot, 30, null));

        var charges = await ChargeCountAsync(factory);
        Assert.Equal(SessionError.NotRunning, extend.Error);
        Assert.Equal(1, charges);
    }

    [Fact]
    public async Task Listener_that_switches_a_relay_does_not_hang()
    {
        using var factory = new VenueAppFactory();
        factory.ExtraServices = services => services.AddScoped<IEventListener, StopOnRequestListener>();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        var queue = factory.Services.GetRequiredService<EventQueue>();

        queue.Publish(new StopRequested(spot));
        var delivering = DeliverAsync(factory);
        await delivering.WaitAsync(FiveSeconds);

        var session = await SingleSessionAsync(factory);
        Assert.False(session.IsRunning);
    }


    private static async Task<(int Panel, int Spot)> AddSpotAsync(VenueAppFactory factory)
    {
        var panel = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, Channel);
        return (panel, spot);
    }

    private static async Task StartAsync(VenueAppFactory factory, int spot)
    {
        var started = await UseSessionsAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        Assert.False(started.IsFailure);
    }

    private static async Task TurnOffBehindTheBackAsync(VenueAppFactory factory, int panel)
    {
        var device = await FindDeviceAsync(factory, panel);
        var allOff = new bool[16];
        factory.Waveshare.SetChannels(device, allOff);
        await CheckAsync(factory);
    }

    private static async Task<Result<Session>> UseSessionsAsync(VenueAppFactory factory, Func<SessionService, Task<Result<Session>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var service = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        return await action(service);
    }

    private static async Task<Result<Spot>> UseSpotsAsync(VenueAppFactory factory, Func<SpotService, Task<Result<Spot>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var codes = new SpotCodeGenerator();
        var sessions = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        var service = ActivatorUtilities.CreateInstance<SpotService>(scope.ServiceProvider, currentActor, codes, sessions);
        return await action(service);
    }

    private static Task TickAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<SessionTickService>(factory.Services).RunOnceAsync();

    private static Task StartupAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<SessionTickService>(factory.Services).RunStartupAsync();

    private static Task CheckAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<DeviceCheckService>(factory.Services).RunOnceAsync();

    private static Task DeliverAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<EventDeliveryService>(factory.Services).RunOnceAsync();

    private static DeviceStatuses Statuses(VenueAppFactory factory) =>
        factory.Services.GetRequiredService<DeviceStatuses>();

    private static async Task<Device> FindDeviceAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Devices.SingleAsync(d => d.Id == id);
    }

    private static async Task<Session> SingleSessionAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Sessions.SingleAsync();
    }

    private static async Task<int> RunningCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Sessions.CountAsync(s => s.EndedAt == null);
    }

    private static async Task<int> ChargeCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.SessionCharges.CountAsync();
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }

    private static async Task<int> CountAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync(e => e.Action == action);
    }

    private record StopRequested(int SpotId) : IVenueEvent;

    private class StopOnRequestListener(SessionService sessions) : IEventListener
    {
        public async Task HandleAsync(IVenueEvent venueEvent, CancellationToken cancellationToken)
        {
            if (venueEvent is StopRequested request)
            {
                await sessions.StopAsync(request.SpotId);
            }
        }
    }
}
