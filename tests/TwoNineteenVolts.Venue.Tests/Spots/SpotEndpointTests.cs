using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Tests.Devices;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class SpotEndpointTests
{
    private const string SupportPassword = "support-password-24-char";

    private record SpotDto(
        int Id,
        string Code,
        string Name,
        int? DeviceId,
        string? DeviceName,
        int? Channel,
        bool? IsOn,
        bool? DeviceOnline,
        DateTime? DeviceLastSeen,
        bool IsArchived,
        DateTime CreatedAt);

    [Fact]
    public async Task Support_can_create_update_new_code_archive_and_switch()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        MarkKnown(factory, panel);
        using var client = await SupportClientAsync(factory);

        var create = await client.PostAsJsonAsync("/api/spots", new { name = "Billiard 1", deviceId = panel, channel = 1 });
        var created = (await create.Content.ReadFromJsonAsync<SpotDto>())!;
        var update = await client.PutAsJsonAsync($"/api/spots/{created.Id}", new { name = "Billiard A", deviceId = panel, channel = 2 });
        var updated = (await update.Content.ReadFromJsonAsync<SpotDto>())!;
        var switchOn = await client.PutAsJsonAsync($"/api/spots/{created.Id}/state", new { on = true });
        var switched = (await switchOn.Content.ReadFromJsonAsync<SpotDto>())!;
        var newCode = await client.PostAsync($"/api/spots/{created.Id}/new-code", null);
        var recoded = (await newCode.Content.ReadFromJsonAsync<SpotDto>())!;
        var archive = await client.PostAsync($"/api/spots/{created.Id}/archive", null);
        var archived = (await archive.Content.ReadFromJsonAsync<SpotDto>())!;

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal($"/api/spots/{created.Id}", create.Headers.Location!.OriginalString);
        Assert.Equal(("Billiard 1", panel, "Panel 1", 1), (created.Name, created.DeviceId, created.DeviceName, created.Channel));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(("Billiard A", 2), (updated.Name, updated.Channel));
        Assert.Equal(HttpStatusCode.OK, switchOn.StatusCode);
        Assert.True(switched.IsOn);
        Assert.Equal(HttpStatusCode.OK, newCode.StatusCode);
        Assert.NotEqual(created.Code, recoded.Code);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.True(archived.IsArchived);
        Assert.Null(archived.DeviceId);
    }

    [Fact]
    public async Task Staff_sees_only_mapped_active_spots_with_device_info()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        MarkKnown(factory, panel);
        var billiard = await TestSpots.AddAsync(factory, "Billiard 1", panel, 2);
        await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        await TestSpots.AddAsync(factory, "Old table", null, null, archived: true);
        var statuses = factory.Services.GetRequiredService<DeviceStatuses>();
        statuses.StoreChannel(panel, 2, true);
        using var client = await StaffClientAsync(factory);

        var spots = (await client.GetFromJsonAsync<List<SpotDto>>("/api/spots"))!;

        var spot = Assert.Single(spots);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        Assert.Equal(billiard, spot.Id);
        Assert.Equal(("Billiard 1", panel, "Panel 1", 2), (spot.Name, spot.DeviceId, spot.DeviceName, spot.Channel));
        Assert.True(spot.IsOn);
        Assert.True(spot.DeviceOnline);
        Assert.Equal(now, spot.DeviceLastSeen);
        Assert.False(spot.IsArchived);
    }

    [Fact]
    public async Task Support_sees_all_spots_with_archived_marked()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await TestSpots.AddAsync(factory, "Billiard 1", panel, 2);
        await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        await TestSpots.AddAsync(factory, "Old table", null, null, archived: true);
        using var client = await SupportClientAsync(factory);

        var spots = (await client.GetFromJsonAsync<List<SpotDto>>("/api/spots"))!;

        var names = spots.Select(s => s.Name);
        var archived = spots.Where(s => s.IsArchived).Select(s => s.Name);
        Assert.Equal(["Billiard 1", "Old table", "Tennis 1"], names);
        Assert.Equal(["Old table"], archived);
    }

    [Fact]
    public async Task Unmapped_spot_in_supports_list_has_empty_device_info()
    {
        using var factory = new VenueAppFactory();
        await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        using var client = await SupportClientAsync(factory);

        var spots = (await client.GetFromJsonAsync<List<SpotDto>>("/api/spots"))!;

        var spot = Assert.Single(spots);
        Assert.Null(spot.DeviceId);
        Assert.Null(spot.DeviceName);
        Assert.Null(spot.Channel);
        Assert.Null(spot.IsOn);
        Assert.Null(spot.DeviceOnline);
        Assert.Null(spot.DeviceLastSeen);
    }

    [Fact]
    public async Task Staff_can_switch_but_gets_403_on_setup()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        using var client = await StaffClientAsync(factory);

        var switchOn = await client.PutAsJsonAsync($"/api/spots/{id}/state", new { on = true });
        var create = await client.PostAsJsonAsync("/api/spots", new { name = "Billiard 2", deviceId = (int?)null, channel = (int?)null });
        var update = await client.PutAsJsonAsync($"/api/spots/{id}", new { name = "Billiard A", deviceId = panel, channel = 1 });
        var newCode = await client.PostAsync($"/api/spots/{id}/new-code", null);
        var archive = await client.PostAsync($"/api/spots/{id}/archive", null);

        Assert.Equal(HttpStatusCode.OK, switchOn.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, newCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task Manager_can_rename_but_gets_403_on_create_new_code_and_archive()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        using var client = await ManagerClientAsync(factory);

        var rename = await client.PutAsJsonAsync($"/api/spots/{id}", new { name = "Billiard A", deviceId = panel, channel = 1 });
        var create = await client.PostAsJsonAsync("/api/spots", new { name = "Billiard 2", deviceId = (int?)null, channel = (int?)null });
        var newCode = await client.PostAsync($"/api/spots/{id}/new-code", null);
        var archive = await client.PostAsync($"/api/spots/{id}/archive", null);

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, newCode.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task Manager_changing_mapping_gets_403()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        using var client = await ManagerClientAsync(factory);

        var update = await client.PutAsJsonAsync($"/api/spots/{id}", new { name = "Billiard A", deviceId = panel, channel = 2 });

        await AssertProblemAsync(update, HttpStatusCode.Forbidden, SpotError.OnlyNameChangeAllowed);
    }

    [Fact]
    public async Task Spot_list_shows_unknown_state_while_device_offline()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        MarkKnown(factory, panel);
        var statuses = factory.Services.GetRequiredService<DeviceStatuses>();
        for (var miss = 1; miss <= 3; miss++)
        {
            statuses.RecordOutcome(panel, DeviceError.NoAnswer);
        }

        using var client = await StaffClientAsync(factory);

        var spots = (await client.GetFromJsonAsync<List<SpotDto>>("/api/spots"))!;

        var spot = Assert.Single(spots);
        Assert.Null(spot.IsOn);
        Assert.False(spot.DeviceOnline);
    }

    [Fact]
    public async Task Failed_switch_returns_504_or_502_with_code()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        using var client = await StaffClientAsync(factory);

        factory.Waveshare.Error = DeviceError.NoAnswer;
        var noAnswer = await client.PutAsJsonAsync($"/api/spots/{id}/state", new { on = true });
        factory.Waveshare.Error = DeviceError.ErrorAnswer;
        var errorAnswer = await client.PutAsJsonAsync($"/api/spots/{id}/state", new { on = true });

        await AssertProblemAsync(noAnswer, HttpStatusCode.GatewayTimeout, DeviceError.NoAnswer);
        await AssertProblemAsync(errorAnswer, HttpStatusCode.BadGateway, DeviceError.ErrorAnswer);
    }

    [Fact]
    public async Task Mismatch_returns_502_with_real_state_in_isOn()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);
        MarkKnown(factory, panel);
        factory.Waveshare.IgnoreSet = true;
        using var client = await StaffClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/spots/{id}/state", new { on = true });

        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var isOn = (JsonElement)problem.Extensions["isOn"]!;
        var code = problem.Extensions["code"]!.ToString();
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(SpotError.StateMismatch, code);
        Assert.Equal(JsonValueKind.False, isOn.ValueKind);
    }

    [Fact]
    public async Task Hidden_spot_returns_404_for_staff()
    {
        using var factory = new VenueAppFactory();
        var unmapped = await TestSpots.AddAsync(factory, "Tennis 1", null, null);
        var archived = await TestSpots.AddAsync(factory, "Old table", null, null, archived: true);
        using var client = await StaffClientAsync(factory);

        var unmappedSwitch = await client.PutAsJsonAsync($"/api/spots/{unmapped}/state", new { on = true });
        var archivedSwitch = await client.PutAsJsonAsync($"/api/spots/{archived}/state", new { on = true });

        await AssertProblemAsync(unmappedSwitch, HttpStatusCode.NotFound, SpotError.NotFound);
        await AssertProblemAsync(archivedSwitch, HttpStatusCode.NotFound, SpotError.NotFound);
    }


    private static Task<int> AddPanelAsync(VenueAppFactory factory)
    {
        return TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
    }

    private static void MarkKnown(VenueAppFactory factory, int deviceId)
    {
        var statuses = factory.Services.GetRequiredService<DeviceStatuses>();
        var allOff = new bool[16];
        statuses.RecordOutcome(deviceId, error: null);
        statuses.StoreChannels(deviceId, allOff);
    }

    private static async Task<HttpClient> SupportClientAsync(VenueAppFactory factory)
    {
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, SupportPassword);
        var client = factory.CreateClient();
        var login = await client.SupportLoginAsync(SupportPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task<HttpClient> ManagerClientAsync(VenueAppFactory factory)
    {
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var client = factory.CreateClient();
        var login = await client.LoginAsync(nino, "1234");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task<HttpClient> StaffClientAsync(VenueAppFactory factory)
    {
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        var client = factory.CreateClient();
        var login = await client.LoginAsync(gio, "5678");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var problemCode = problem.Extensions["code"]!.ToString();
        Assert.Equal(code, problemCode);
    }
}
