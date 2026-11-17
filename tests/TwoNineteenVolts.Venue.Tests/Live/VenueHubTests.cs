using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Groups;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Events;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Events;
using TwoNineteenVolts.Venue.Tests.Groups;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Live;

public class VenueHubTests
{
    private const int MarkerSpotId = -1;

    [Fact]
    public async Task Anonymous_cannot_connect()
    {
        using var factory = new VenueAppFactory();

        var connecting = HubClient.ConnectAsync(factory, cookie: null);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => connecting);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Switching_a_spot_pushes_spot_changed()
    {
        using var factory = new VenueAppFactory();
        var panel = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        var (client, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);

        await client.PutAsJsonAsync($"/api/spots/{spot}/state", new { isOn = true });
        await DeliverAsync(factory);

        var changed = await hub.NextSpotChangeAsync();
        Assert.Equal(spot, changed);
    }

    [Fact]
    public async Task Device_going_offline_pushes_every_spot_on_it()
    {
        using var factory = new VenueAppFactory();
        var panel = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        var first = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        var second = await TestSpots.AddAsync(factory, "Billiard 2", panel, 2);
        var (_, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);
        await CheckAsync(factory);
        PublishedEvents.Take(factory);
        factory.Waveshare.Error = DeviceError.NoAnswer;

        for (var miss = 1; miss <= 3; miss++)
        {
            await CheckAsync(factory);
        }

        await DeliverAsync(factory);
        var firstChange = await hub.NextSpotChangeAsync();
        var secondChange = await hub.NextSpotChangeAsync();
        int[] changes = [firstChange, secondChange];
        var sorted = changes.Order().ToList();
        Assert.Equal([first, second], sorted);
    }

    [Fact]
    public async Task Last_seen_only_publishes_nothing()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        await CheckAsync(factory);
        PublishedEvents.Take(factory);
        factory.Time.Advance(TimeSpan.FromSeconds(10));

        await CheckAsync(factory);

        var events = PublishedEvents.Take(factory);
        Assert.Empty(events);
    }

    [Fact]
    public async Task Hub_stays_open_until_the_login_ends()
    {
        using var factory = new VenueAppFactory();
        var (client, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);

        await client.PostAsync("/api/auth/logout", content: null);
        var closedBeforeDelivery = await hub.ClosesWithinAsync(TimeSpan.FromSeconds(2));
        await DeliverAsync(factory);

        Assert.False(closedBeforeDelivery);
        await hub.WaitUntilClosedAsync();
    }

    [Fact]
    public async Task Channel_change_found_by_a_check_pushes_only_that_spot()
    {
        using var factory = new VenueAppFactory();
        var panelId = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        await TestSpots.AddAsync(factory, "Billiard 1", panelId, 1);
        var second = await TestSpots.AddAsync(factory, "Billiard 2", panelId, 2);
        var (_, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);
        await CheckAsync(factory);
        PublishedEvents.Take(factory);
        var panel = await FindDeviceAsync(factory, panelId);
        factory.Waveshare.SetChannels(panel, false, true, false, false, false, false, false, false);

        await CheckAsync(factory);
        var pushed = await PushedUntilMarkerAsync(factory, hub);

        Assert.Equal([second], pushed);
    }

    [Fact]
    public async Task Deleting_a_device_pushes_its_spots()
    {
        using var factory = new VenueAppFactory();
        var panelId = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        var first = await TestSpots.AddAsync(factory, "Billiard 1", panelId, 1);
        var second = await TestSpots.AddAsync(factory, "Billiard 2", panelId, 2);
        var (_, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);

        using (var scope = factory.Services.CreateScope())
        {
            var devices = scope.ServiceProvider.GetRequiredService<DeviceService>();
            await devices.DeleteAsync(panelId);
        }

        var pushed = await PushedUntilMarkerAsync(factory, hub);
        Assert.Equal([first, second], pushed);
    }

    [Fact]
    public async Task Renaming_a_group_pushes_its_spots()
    {
        using var factory = new VenueAppFactory();
        var billiards = await TestGroups.AddAsync(factory, "Billiards");
        var pool = await TestGroups.AddAsync(factory, "Pool");
        var first = await TestSpots.AddAsync(factory, "Billiard 1", null, null, groupId: billiards);
        await TestSpots.AddAsync(factory, "Pool 1", null, null, groupId: pool);
        var (_, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);
        var queue = factory.Services.GetRequiredService<EventQueue>();

        queue.Publish(new GroupRenamed(billiards));
        var pushed = await PushedUntilMarkerAsync(factory, hub);

        Assert.Equal([first], pushed);
    }

    [Fact]
    public async Task Logout_closes_the_hub()
    {
        using var factory = new VenueAppFactory();
        var (client, cookie) = await LoginAsync(factory, "Gio", Role.Staff);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);

        await client.PostAsync("/api/auth/logout", content: null);
        await DeliverAsync(factory);

        await hub.WaitUntilClosedAsync();
    }

    [Fact]
    public async Task Deactivated_user_hub_closes_and_cannot_reconnect()
    {
        using var factory = new VenueAppFactory();
        var (manager, _) = await LoginAsync(factory, "Nino", Role.Manager);
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "1234");
        using var gioClient = factory.CreateClient();
        var gioLogin = await gioClient.LoginAsync(gio, "1234");
        var cookie = HubClient.CookieOf(gioLogin);
        await using var hub = await HubClient.ConnectAsync(factory, cookie);

        await manager.PostAsync($"/api/users/{gio}/deactivate", content: null);
        await DeliverAsync(factory);
        await hub.WaitUntilClosedAsync();

        var reconnecting = HubClient.ConnectAsync(factory, cookie);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => reconnecting);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }


    private static async Task<(HttpClient Client, string Cookie)> LoginAsync(VenueAppFactory factory, string name, Role role)
    {
        UseRealClock(factory);
        var id = await TestUsers.AddAsync(factory, name, role, "1234");
        var client = factory.CreateClient();
        var login = await client.LoginAsync(id, "1234");
        var cookie = HubClient.CookieOf(login);
        return (client, cookie);
    }

    private static async Task<List<int>> PushedUntilMarkerAsync(VenueAppFactory factory, HubClient hub)
    {
        var queue = factory.Services.GetRequiredService<EventQueue>();
        queue.Publish(new SpotUpdated(MarkerSpotId));
        await DeliverAsync(factory);

        var pushed = new List<int>();
        var spotId = await hub.NextSpotChangeAsync();
        while (spotId != MarkerSpotId)
        {
            pushed.Add(spotId);
            spotId = await hub.NextSpotChangeAsync();
        }

        return pushed;
    }

    private static async Task<Device> FindDeviceAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Devices.SingleAsync(d => d.Id == id);
    }

    private static void UseRealClock(VenueAppFactory factory)
    {
        var realNow = DateTimeOffset.UtcNow;
        if (factory.Time.GetUtcNow() < realNow)
        {
            factory.Time.SetUtcNow(realNow);
        }
    }

    private static Task DeliverAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<EventDeliveryService>(factory.Services).RunOnceAsync();

    private static Task CheckAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<DeviceCheckService>(factory.Services).RunOnceAsync();
}
