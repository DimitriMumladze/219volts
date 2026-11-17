using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Sessions;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Groups;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Sessions;

public class SessionRulesTests
{
    private const int Channel = 3;

    private static readonly AuditActor Support = new(1, "219volts Support", Role.Support);
    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);

    [Fact]
    public async Task Manual_off_ends_the_session()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);

        var result = await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, false));

        var session = await SingleSessionAsync(factory);
        var entry = await SingleAuditAsync(factory, AuditActions.SessionEnded);
        Assert.False(result.IsFailure);
        Assert.False(session.IsRunning);
        Assert.Equal(SessionEndReason.ManualOff, session.EndReason);
        Assert.Equal("Gio", session.EndedByName);
        Assert.Equal("manual off", entry.Details);
    }

    [Fact]
    public async Task Failed_manual_off_keeps_the_session()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Waveshare.Error = DeviceError.NoAnswer;

        var result = await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, false));

        var session = await SingleSessionAsync(factory);
        Assert.Equal(DeviceError.NoAnswer, result.Error);
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task Manual_off_right_after_the_timer_ended_the_session_just_switches_off()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Time.Advance(TimeSpan.FromMinutes(60));
        await ActivatorUtilities.CreateInstance<SessionTickService>(factory.Services).RunOnceAsync();
        await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, true));

        var result = await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, false));

        var ended = await CountAuditAsync(factory, AuditActions.SessionEnded);
        var offCalls = factory.Waveshare.Calls.Count(c => c == $"set {panel}/{Channel} off");
        Assert.False(result.IsFailure);
        Assert.Equal(2, offCalls);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task Disobeying_manual_off_keeps_the_session_running()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        factory.Waveshare.ShouldIgnoreSet = true;

        var result = await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, false));

        var session = await SingleSessionAsync(factory);
        Assert.Equal(DeviceError.DidNotObey, result.Error);
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task Manual_on_does_not_touch_the_session()
    {
        using var factory = new VenueAppFactory();
        var (_, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);

        await UseSpotsAsync(factory, Staff, spots => spots.SwitchAsync(spot, true));

        var session = await SingleSessionAsync(factory);
        Assert.True(session.IsRunning);
    }

    [Fact]
    public async Task Remap_archive_and_device_delete_are_refused_while_running()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        var group = await TestGroups.DefaultAsync(factory);
        await StartAsync(factory, spot);

        var remap = await UseSpotsAsync(factory, Support, spots => spots.UpdateAsync(spot, "Billiard 1", group, panel, 4));
        var archive = await UseSpotsAsync(factory, Support, spots => spots.ArchiveAsync(spot));
        var delete = await UseDevicesAsync(factory, devices => devices.DeleteAsync(panel));

        Assert.Equal(SpotError.SessionRunning, remap.Error);
        Assert.Equal(SpotError.SessionRunning, archive.Error);
        Assert.Equal(DeviceError.SessionRunning, delete.Error);
    }

    [Fact]
    public async Task Rename_and_group_move_are_allowed_while_running()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        var pool = await TestGroups.AddAsync(factory, "Pool");
        await StartAsync(factory, spot);

        var result = await UseSpotsAsync(factory, Support, spots => spots.UpdateAsync(spot, "Billiard A", pool, panel, Channel));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task Setup_is_allowed_again_after_the_session_ends()
    {
        using var factory = new VenueAppFactory();
        var (panel, spot) = await AddSpotAsync(factory);
        await StartAsync(factory, spot);
        await UseSessionsAsync(factory, sessions => sessions.StopAsync(spot));

        var archive = await UseSpotsAsync(factory, Support, spots => spots.ArchiveAsync(spot));
        var delete = await UseDevicesAsync(factory, devices => devices.DeleteAsync(panel));

        Assert.False(archive.IsFailure);
        Assert.False(delete.IsFailure);
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

    private static async Task<Result<Session>> UseSessionsAsync(VenueAppFactory factory, Func<SessionService, Task<Result<Session>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var service = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        return await action(service);
    }

    private static async Task<Result<Spot>> UseSpotsAsync(VenueAppFactory factory, AuditActor actor, Func<SpotService, Task<Result<Spot>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(actor);
        var codes = new SpotCodeGenerator();
        var sessions = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        var service = ActivatorUtilities.CreateInstance<SpotService>(scope.ServiceProvider, currentActor, codes, sessions);
        return await action(service);
    }

    private static async Task<Result<Device>> UseDevicesAsync(VenueAppFactory factory, Func<DeviceService, Task<Result<Device>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DeviceService>();
        return await action(service);
    }

    private static async Task<Session> SingleSessionAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Sessions.SingleAsync();
    }

    private static async Task<int> CountAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync(e => e.Action == action);
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }
}
