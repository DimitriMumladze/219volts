using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceEndpointTests
{
    private const string SupportPassword = "support-password-24-char";

    private record DeviceDto(
        int Id,
        string Name,
        string Kind,
        string IpAddress,
        int Port,
        int ChannelCount,
        bool IsOnline,
        DateTime? LastSeen,
        DateTime CreatedAt);

    [Fact]
    public async Task Support_can_register_list_update_and_delete()
    {
        using var factory = new VenueAppFactory();
        using var client = await SupportClientAsync(factory);

        var register = await client.PostAsJsonAsync("/api/devices", new { name = "Panel 1", kind = "WaveshareModbusTcp", ipAddress = "192.168.10.21", channelCount = 16 });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var panel = (await register.Content.ReadFromJsonAsync<DeviceDto>())!;
        Assert.Equal($"/api/devices/{panel.Id}", register.Headers.Location!.OriginalString);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        Assert.Equal(new DeviceDto(panel.Id, "Panel 1", "WaveshareModbusTcp", "192.168.10.21", 502, 16, false, null, now), panel);

        await client.PostAsJsonAsync("/api/devices", new { name = "Bar Shelly", kind = "ShellyGen2", ipAddress = "192.168.10.30", port = 8080, channelCount = 4 });
        var statuses = factory.Services.GetRequiredService<DeviceStatuses>();
        statuses.RecordAnswer(panel.Id);
        var listed = (await client.GetFromJsonAsync<List<DeviceDto>>("/api/devices"))!;
        var names = listed.Select(d => d.Name);
        var listedPanel = listed.Single(d => d.Id == panel.Id);
        Assert.Equal(["Bar Shelly", "Panel 1"], names);
        Assert.True(listedPanel.IsOnline);
        Assert.Equal(now, listedPanel.LastSeen);

        var update = await client.PutAsJsonAsync($"/api/devices/{panel.Id}", new { name = "Panel A", ipAddress = "192.168.10.22", port = 502 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<DeviceDto>())!;
        Assert.Equal(("Panel A", "192.168.10.22", false), (updated.Name, updated.IpAddress, updated.IsOnline));

        var delete = await client.DeleteAsync($"/api/devices/{panel.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var remaining = (await client.GetFromJsonAsync<List<DeviceDto>>("/api/devices"))!;
        var remainingNames = remaining.Select(d => d.Name);
        Assert.Equal(["Bar Shelly"], remainingNames);
    }

    [Fact]
    public async Task Manager_can_list_but_gets_403_on_changes()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = factory.CreateClient();
        await client.LoginAsync(nino, "1234");

        var list = await client.GetAsync("/api/devices");
        var register = await client.PostAsJsonAsync("/api/devices", new { name = "Panel 2", kind = "ShellyGen2", ipAddress = "192.168.10.30", channelCount = 1 });
        var update = await client.PutAsJsonAsync($"/api/devices/{id}", new { name = "Panel A", ipAddress = "192.168.10.21", port = 502 });
        var delete = await client.DeleteAsync($"/api/devices/{id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Staff_gets_403_on_devices()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = factory.CreateClient();
        await client.LoginAsync(gio, "5678");

        var list = await client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task Not_logged_in_gets_401_on_devices()
    {
        using var factory = new VenueAppFactory();
        using var client = factory.CreateClient();

        var list = await client.GetAsync("/api/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Errors_return_problem_with_code()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
        using var client = await SupportClientAsync(factory);

        var invalidIp = await client.PostAsJsonAsync("/api/devices", new { name = "Panel 2", kind = "ShellyGen2", ipAddress = "::1", channelCount = 1 });
        var duplicateName = await client.PostAsJsonAsync("/api/devices", new { name = "panel 1", kind = "ShellyGen2", ipAddress = "192.168.10.30", channelCount = 1 });
        var unknownDevice = await client.PutAsJsonAsync("/api/devices/999", new { name = "Panel 9", ipAddress = "192.168.10.99", port = 502 });

        await AssertProblemAsync(invalidIp, HttpStatusCode.BadRequest, DeviceError.InvalidIpAddress);
        await AssertProblemAsync(duplicateName, HttpStatusCode.Conflict, DeviceError.DuplicateName);
        await AssertProblemAsync(unknownDevice, HttpStatusCode.NotFound, DeviceError.NotFound);
    }

    [Fact]
    public async Task Unknown_kind_returns_400()
    {
        using var factory = new VenueAppFactory();
        using var client = await SupportClientAsync(factory);

        var register = await client.PostAsJsonAsync("/api/devices", new { name = "Panel 1", kind = "Sonoff", ipAddress = "192.168.10.21", channelCount = 4 });

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(1)]
    public async Task Kind_as_number_returns_400(int kind)
    {
        using var factory = new VenueAppFactory();
        using var client = await SupportClientAsync(factory);

        var register = await client.PostAsJsonAsync("/api/devices", new { name = "Panel 1", kind, ipAddress = "192.168.10.21", channelCount = 16 });

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
    }


    private static async Task<HttpClient> SupportClientAsync(VenueAppFactory factory)
    {
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, SupportPassword);
        var client = factory.CreateClient();
        var login = await client.SupportLoginAsync(SupportPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var problemCode = problem.Extensions["code"]!.ToString();
        var hasTitle = !string.IsNullOrEmpty(problem.Title);
        Assert.Equal(code, problemCode);
        Assert.True(hasTitle);
    }
}
