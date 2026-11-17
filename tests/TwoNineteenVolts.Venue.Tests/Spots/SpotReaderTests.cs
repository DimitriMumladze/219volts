using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Groups;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class SpotReaderTests
{
    private static readonly AuditActor Support = new(1, "219volts Support", Role.Support);
    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);

    [Fact]
    public async Task Staff_sees_only_mapped_active_spots()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        await TestSpots.AddAsync(factory, "Old table", null, null, archived: true);

        var views = await UseReaderAsync(factory, reader => reader.ViewAllAsync(Staff));

        var names = views.Select(v => v.Name).ToList();
        Assert.Equal(["Billiard 1"], names);
    }

    [Fact]
    public async Task Staff_does_not_see_an_archived_spot_even_if_still_mapped()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        await ArchiveKeepingTheMappingAsync(factory, spot);

        var staffList = await UseReaderAsync(factory, reader => reader.ViewAllAsync(Staff));
        var staffView = await UseReaderAsync(factory, reader => reader.ViewAsync(spot, Staff));
        var supportView = await UseReaderAsync(factory, reader => reader.ViewAsync(spot, Support));

        Assert.Empty(staffList);
        Assert.Null(staffView);
        Assert.NotNull(supportView);
    }

    [Fact]
    public async Task Support_sees_all_spots_by_name()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        await TestSpots.AddAsync(factory, "Old table", null, null, archived: true);

        var views = await UseReaderAsync(factory, reader => reader.ViewAllAsync(Support));

        var names = views.Select(v => v.Name).ToList();
        Assert.Equal(["Billiard 1", "Old table", "Tennis 1"], names);
    }

    [Fact]
    public async Task Hidden_spot_view_is_null_for_staff()
    {
        using var factory = new VenueAppFactory();
        var tennis = await TestSpots.AddAsync(factory, "Tennis 1", null, null);

        var view = await UseReaderAsync(factory, reader => reader.ViewAsync(tennis, Staff));
        var entity = await UseReaderAsync(factory, reader => reader.FindVisibleAsync(tennis, Staff));
        var forSupport = await UseReaderAsync(factory, reader => reader.ViewAsync(tennis, Support));

        Assert.Null(view);
        Assert.Null(entity);
        Assert.NotNull(forSupport);
    }

    [Fact]
    public async Task View_shows_group_device_state_session_total_and_warning()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var group = await TestGroups.AddAsync(factory, "Billiards", 1000);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3, groupId: group);
        await UseSessionsAsync(factory, sessions => sessions.StartAsync(spot, 60, null));
        await UseSessionsAsync(factory, sessions => sessions.ExtendAsync(spot, 30, null));
        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        warnings.Add(spot);
        var now = factory.Time.GetUtcNow().UtcDateTime;

        var view = await UseReaderAsync(factory, reader => reader.ViewAsync(spot, Staff));

        Assert.NotNull(view);
        Assert.Equal("Billiards", view.GroupName);
        Assert.Equal("Panel 1", view.DeviceName);
        Assert.Equal(3, view.Channel);
        Assert.True(view.IsOn);
        Assert.True(view.DeviceOnline);
        Assert.Equal(now, view.DeviceLastSeen);
        Assert.Equal(1500, view.Session!.TotalChargedTetri);
        Assert.Equal("Gio", view.Session.StartedBy);
        Assert.True(view.SwitchOffFailed);
    }

    [Fact]
    public async Task View_of_spot_whose_device_vanished_has_empty_device_fields()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        await DeleteDeviceBehindTheBackAsync(factory, panel);

        var view = await UseReaderAsync(factory, reader => reader.ViewAsync(spot, Support));

        Assert.NotNull(view);
        Assert.Null(view.DeviceId);
        Assert.Null(view.DeviceName);
        Assert.Null(view.Channel);
        Assert.Null(view.IsOn);
        Assert.Null(view.DeviceOnline);
        Assert.Null(view.DeviceLastSeen);
        Assert.False(view.SwitchOffFailed);
    }


    private static Task<int> AddPanelAsync(VenueAppFactory factory)
    {
        return TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
    }

    private static async Task ArchiveKeepingTheMappingAsync(VenueAppFactory factory, int spotId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE Spots SET IsArchived = 1 WHERE Id = {spotId}");
    }

    private static async Task DeleteDeviceBehindTheBackAsync(VenueAppFactory factory, int deviceId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF");
        await db.Database.ExecuteSqlAsync($"DELETE FROM Devices WHERE Id = {deviceId}");
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON");
        await db.Database.CloseConnectionAsync();
    }

    private static async Task<T> UseReaderAsync<T>(VenueAppFactory factory, Func<SpotReader, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<SpotReader>();
        return await action(reader);
    }

    private static async Task UseSessionsAsync(VenueAppFactory factory, Func<SessionService, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(Staff);
        var sessions = ActivatorUtilities.CreateInstance<SessionService>(scope.ServiceProvider, currentActor);
        await action(sessions);
    }
}
