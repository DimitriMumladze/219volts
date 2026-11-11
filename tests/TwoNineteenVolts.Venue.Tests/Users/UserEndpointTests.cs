using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Users;

public class UserEndpointTests
{
    private record Me(int Id, string DisplayName, string Role);

    private record UserDto(int Id, string DisplayName, string Role, bool IsActive, bool RemoteAccessAllowed, DateTime CreatedAt);

    [Fact]
    public async Task Staff_gets_403_on_users()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = factory.CreateClient();
        await client.LoginAsync(gio, "5678");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/users", new { displayName = "Eve", role = "Manager", pin = "1234" })).StatusCode);
    }

    [Fact]
    public async Task Not_logged_in_gets_401_on_users()
    {
        using var factory = new VenueAppFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Manager_can_create_list_and_deactivate()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "support-password");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var create = await client.PostAsJsonAsync("/api/users", new { displayName = "Gio", role = "Staff", pin = "5678" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var gio = (await create.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal($"/api/users/{gio.Id}", create.Headers.Location!.OriginalString);
        Assert.Equal(new UserDto(gio.Id, "Gio", "Staff", true, false, factory.Time.GetUtcNow().UtcDateTime), gio);

        var listed = await client.GetFromJsonAsync<List<UserDto>>("/api/users");
        Assert.Equal(["Gio", "Nino"], listed!.Select(u => u.DisplayName));

        var deactivate = await client.PostAsync($"/api/users/{gio.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.False((await deactivate.Content.ReadFromJsonAsync<UserDto>())!.IsActive);
    }

    [Fact]
    public async Task Manager_can_rename_change_role_set_pin_and_remote_access()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = await LoggedInAsync(factory, nino, "1234");

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{gio}/name", new { displayName = "Giorgi" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{gio}/role", new { role = "Manager" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/users/{gio}/pin", new { pin = "0000" })).StatusCode);
        var remote = await client.PutAsJsonAsync($"/api/users/{gio}/remote-access", new { allowed = true });

        var updated = (await remote.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(("Giorgi", "Manager", true), (updated.DisplayName, updated.Role, updated.RemoteAccessAllowed));
        using var giorgi = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await giorgi.LoginAsync(gio, "0000")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_users_next_request_gets_401()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var manager = await LoggedInAsync(factory, nino, "1234");
        using var staff = await LoggedInAsync(factory, gio, "5678");
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/auth/me")).StatusCode);

        await manager.PostAsync($"/api/users/{gio}/deactivate", null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Renamed_user_logs_in_again_and_is_audited_under_the_new_name()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var manager = await LoggedInAsync(factory, nino, "1234");
        using var staff = await LoggedInAsync(factory, gio, "5678");

        await manager.PutAsJsonAsync($"/api/users/{gio}/name", new { displayName = "Giorgi" });

        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.LoginAsync(gio, "5678")).StatusCode);
        var me = await staff.GetFromJsonAsync<Me>("/api/auth/me");
        Assert.Equal("Giorgi", me!.DisplayName);

        await staff.PostAsync("/api/auth/logout", null);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var logout = await db.AuditEntries.SingleAsync(e => e.Action == AuditActions.Logout);
        Assert.Equal("Giorgi", logout.ActorName);
    }

    [Fact]
    public async Task Last_manager_cannot_demote_or_deactivate_themselves()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = await LoggedInAsync(factory, nino, "1234");

        await AssertProblemAsync(await client.PutAsJsonAsync($"/api/users/{nino}/role", new { role = "Staff" }), HttpStatusCode.Conflict);
        await AssertProblemAsync(await client.PostAsync($"/api/users/{nino}/deactivate", null), HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_returns_409_problem()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var create = await client.PostAsJsonAsync("/api/users", new { displayName = " NINO ", role = "Staff", pin = "5678" });

        await AssertProblemAsync(create, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bad_input_returns_400_and_support_returns_404()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "support-password");
        using var client = await LoggedInAsync(factory, nino, "1234");

        await AssertProblemAsync(await client.PostAsJsonAsync("/api/users", new { displayName = "Gio", role = "Staff", pin = "12a4" }), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/users", new { displayName = "  ", role = "Staff", pin = "1234" }), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await client.PostAsJsonAsync("/api/users", new { displayName = "Gio", role = "Support", pin = "1234" }), HttpStatusCode.BadRequest);
        await AssertProblemAsync(await client.PutAsJsonAsync($"/api/users/{support}/pin", new { pin = "1234" }), HttpStatusCode.NotFound);
        Assert.DoesNotContain(await client.GetFromJsonAsync<List<UserDto>>("/api/users") ?? [], u => u.Id == support);
    }

    private static async Task<HttpClient> LoggedInAsync(VenueAppFactory factory, int userId, string pin)
    {
        var client = factory.CreateClient();
        var login = await client.LoginAsync(userId, pin);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)expected, problem!.Status);
        Assert.False(string.IsNullOrEmpty(problem.Title));
    }
}
