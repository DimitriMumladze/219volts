using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Users.Contracts;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Users;

public class UserEndpointTests
{
    private record Me(int Id, string DisplayName, string Role);

    private record UserDto(int Id, string DisplayName, string Role, bool IsActive, DateTime CreatedAt);

    [Fact]
    public async Task Staff_gets_403_on_users()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = factory.CreateClient();
        await client.LoginAsync(gio, "5678");

        var list = await client.GetAsync("/api/users");
        var create = await client.PostAsJsonAsync("/api/users", new { displayName = "Eve", pin = "1234" });
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Not_logged_in_gets_401_on_users()
    {
        using var factory = new VenueAppFactory();
        using var client = factory.CreateClient();

        var list = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    [Fact]
    public async Task Manager_can_create_list_and_deactivate()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "support-password");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var create = await client.PostAsJsonAsync("/api/users", new { displayName = "Gio", pin = "5678" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var gio = (await create.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal($"/api/users/{gio.Id}", create.Headers.Location!.OriginalString);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        Assert.Equal(new UserDto(gio.Id, "Gio", "Staff", true, now), gio);

        var listed = await client.GetFromJsonAsync<List<UserDto>>("/api/users");
        var names = listed!.Select(u => u.DisplayName);
        Assert.Equal(["Gio", "Nino"], names);

        var deactivate = await client.PostAsync($"/api/users/{gio.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = (await deactivate.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.False(deactivated.IsActive);
    }

    [Fact]
    public async Task Manager_can_rename_and_set_pin()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var rename = await client.PutAsJsonAsync($"/api/users/{gio}/name", new { displayName = "Giorgi" });
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        var pin = await client.PutAsJsonAsync($"/api/users/{gio}/pin", new { pin = "0000" });

        var renamed = (await pin.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal("Giorgi", renamed.DisplayName);
        using var giorgi = factory.CreateClient();
        var login = await giorgi.LoginAsync(gio, "0000");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Deactivated_users_next_request_gets_401()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var manager = await LoggedInAsync(factory, nino, "1234");
        using var staff = await LoggedInAsync(factory, gio, "5678");
        var before = await staff.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        await manager.PostAsync($"/api/users/{gio}/deactivate", null);

        var after = await staff.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
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

        var afterRename = await staff.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterRename.StatusCode);
        var login = await staff.LoginAsync(gio, "5678");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var me = await staff.GetFromJsonAsync<Me>("/api/auth/me");
        Assert.Equal("Giorgi", me!.DisplayName);

        await staff.PostAsync("/api/auth/logout", null);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var logout = await db.AuditEntries.SingleAsync(e => e.Action == AuditActions.Logout);
        Assert.Equal("Giorgi", logout.ActorName);
    }

    [Fact]
    public async Task Manager_cannot_deactivate_themselves()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var deactivate = await client.PostAsync($"/api/users/{nino}/deactivate", null);

        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        var problem = (await deactivate.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal("The manager cannot be deactivated", problem.Title);
        var code = problem.Extensions["code"]!.ToString();
        Assert.Equal(UserError.ManagerCannotBeDeactivated, code);
        Assert.Equal("UserManagerCannotBeDeactivated", UserError.ManagerCannotBeDeactivated);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Duplicate_name_returns_409_problem()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var create = await client.PostAsJsonAsync("/api/users", new { displayName = " NINO ", pin = "5678" });

        await AssertProblemAsync(create, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bad_input_returns_400_and_support_returns_404()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var support = await TestUsers.AddAsync(factory, "219volts Support", Role.Support, "support-password");
        using var client = await LoggedInAsync(factory, nino, "1234");

        var badPin = await client.PostAsJsonAsync("/api/users", new { displayName = "Gio", pin = "12a4" });
        var emptyName = await client.PostAsJsonAsync("/api/users", new { displayName = "  ", pin = "1234" });
        var supportPin = await client.PutAsJsonAsync($"/api/users/{support}/pin", new { pin = "1234" });
        var listed = await client.GetFromJsonAsync<List<UserDto>>("/api/users") ?? [];

        await AssertProblemAsync(badPin, HttpStatusCode.BadRequest);
        await AssertProblemAsync(emptyName, HttpStatusCode.BadRequest);
        await AssertProblemAsync(supportPin, HttpStatusCode.NotFound);
        Assert.DoesNotContain(listed, u => u.Id == support);
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
        var hasTitle = !string.IsNullOrEmpty(problem!.Title);
        Assert.Equal((int)expected, problem.Status);
        Assert.True(hasTitle);
    }
}
