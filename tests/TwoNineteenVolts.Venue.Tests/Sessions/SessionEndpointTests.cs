using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Sessions.Contracts;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Tests.Devices;
using TwoNineteenVolts.Venue.Tests.Groups;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Sessions;

public class SessionEndpointTests
{
    private const int Channel = 3;

    private record SessionDto(int Id, DateTime StartedAt, DateTime EndsAt, string StartedBy, decimal TotalCharged);

    private record SpotDto(int Id, string Name, bool? IsOn, SessionDto? Session, bool HasSwitchOffFailed);

    private record PriceDto(decimal Amount);

    [Fact]
    public async Task Start_extend_stop_over_http_return_the_spot()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        using var client = await StaffClientAsync(factory);

        var start = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });
        var started = (await start.Content.ReadFromJsonAsync<SpotDto>())!;
        var extend = await client.PostAsJsonAsync($"/api/spots/{spot}/session/extend", new { minutes = 30, amount = 0m });
        var extended = (await extend.Content.ReadFromJsonAsync<SpotDto>())!;
        var stop = await client.PostAsync($"/api/spots/{spot}/session/stop", content: null);
        var stopped = (await stop.Content.ReadFromJsonAsync<SpotDto>())!;

        var endsAt = now.AddMinutes(60);
        var extendedEndsAt = now.AddMinutes(90);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.Equal(spot, started.Id);
        Assert.True(started.IsOn);
        Assert.Equal(endsAt, started.Session!.EndsAt);
        Assert.Equal("Gio", started.Session.StartedBy);
        Assert.Equal(10m, started.Session.TotalCharged);
        Assert.Equal(HttpStatusCode.OK, extend.StatusCode);
        Assert.Equal(extendedEndsAt, extended.Session!.EndsAt);
        Assert.Equal(10m, extended.Session.TotalCharged);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Null(stopped.Session);
        Assert.False(stopped.IsOn);
    }

    [Fact]
    public async Task Spot_list_shows_the_running_session_with_its_total()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = await StaffClientAsync(factory);
        await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });
        await client.PostAsJsonAsync($"/api/spots/{spot}/session/extend", new { minutes = 30 });

        var spots = (await client.GetFromJsonAsync<List<SpotDto>>("/api/spots"))!;

        var shown = Assert.Single(spots);
        Assert.Equal(15m, shown.Session!.TotalCharged);
        Assert.False(shown.HasSwitchOffFailed);
    }

    [Fact]
    public async Task Price_endpoint_returns_1_17_for_7_minutes_at_10()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = await StaffClientAsync(factory);

        var price = await client.GetFromJsonAsync<PriceDto>($"/api/spots/{spot}/price?minutes=7");

        Assert.Equal(1.17m, price!.Amount);
    }

    [Fact]
    public async Task Anonymous_cannot_run_sessions()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = factory.CreateClient();

        var start = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });
        var price = await client.GetAsync($"/api/spots/{spot}/price?minutes=7");

        Assert.Equal(HttpStatusCode.Unauthorized, start.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, price.StatusCode);
    }

    [Fact]
    public async Task Errors_have_codes_and_statuses()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = await StaffClientAsync(factory);

        var badMinutes = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 601 });
        var badAmount = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60, amount = 10.01m });
        var notRunning = await client.PostAsync($"/api/spots/{spot}/session/stop", content: null);
        await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });
        var busy = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });
        var badPrice = await client.GetAsync($"/api/spots/{spot}/price?minutes=0");
        var missing = await client.PostAsJsonAsync("/api/spots/99/session", new { minutes = 60 });

        await AssertProblemAsync(badMinutes, HttpStatusCode.BadRequest, SessionError.InvalidMinutes);
        await AssertProblemAsync(badAmount, HttpStatusCode.BadRequest, SessionError.InvalidAmount);
        await AssertProblemAsync(notRunning, HttpStatusCode.Conflict, SessionError.NotRunning);
        await AssertProblemAsync(busy, HttpStatusCode.Conflict, SessionError.AlreadyRunning);
        await AssertProblemAsync(badPrice, HttpStatusCode.BadRequest, SessionError.InvalidMinutes);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "SpotNotFound");
    }

    [Fact]
    public async Task Session_mismatch_includes_the_relay_state()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = await StaffClientAsync(factory);
        factory.Waveshare.ShouldIgnoreSet = true;

        var start = await client.PostAsJsonAsync($"/api/spots/{spot}/session", new { minutes = 60 });

        var problem = (await start.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var isOn = problem.Extensions["isOn"]!.ToString();
        Assert.Equal(HttpStatusCode.BadGateway, start.StatusCode);
        Assert.Equal("False", isOn);
    }

    [Fact]
    public async Task Switch_off_warning_shows_while_the_relay_is_not_off()
    {
        using var factory = new VenueAppFactory();
        var spot = await AddSpotAsync(factory);
        using var client = await StaffClientAsync(factory);
        await client.PutAsJsonAsync($"/api/spots/{spot}/state", new { isOn = true });
        var warnings = factory.Services.GetRequiredService<SwitchOffWarnings>();
        warnings.Add(spot);

        var whileOn = await client.GetFromJsonAsync<SpotDto>($"/api/spots/{spot}");
        await client.PutAsJsonAsync($"/api/spots/{spot}/state", new { isOn = false });
        var whenOff = await client.GetFromJsonAsync<SpotDto>($"/api/spots/{spot}");

        Assert.True(whileOn!.HasSwitchOffFailed);
        Assert.False(whenOff!.HasSwitchOffFailed);
    }


    private static async Task<int> AddSpotAsync(VenueAppFactory factory)
    {
        var panel = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
        var group = await TestGroups.AddAsync(factory, "Billiards", 1000);
        return await TestSpots.AddAsync(factory, "Billiard 1", panel, Channel, groupId: group);
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
