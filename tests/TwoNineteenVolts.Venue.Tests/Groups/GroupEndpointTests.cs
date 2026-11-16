using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TwoNineteenVolts.Venue.Application.Groups;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Tests.Groups;

public class GroupEndpointTests
{
    private const string SupportPassword = "support-password-24-char";

    private record GroupDto(int Id, string Name, decimal HourlyRate, bool IsArchived);

    [Fact]
    public async Task Support_can_create_and_archive()
    {
        using var factory = new VenueAppFactory();
        using var client = await SupportClientAsync(factory);

        var create = await client.PostAsJsonAsync("/api/groups", new { name = "Billiards", hourlyRate = 10.5m });
        var created = (await create.Content.ReadFromJsonAsync<GroupDto>())!;
        var archive = await client.PostAsync($"/api/groups/{created.Id}/archive", content: null);
        var archived = (await archive.Content.ReadFromJsonAsync<GroupDto>())!;

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(new GroupDto(created.Id, "Billiards", 10.5m, false), created);
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        Assert.True(archived.IsArchived);
    }

    [Fact]
    public async Task Manager_can_rename_and_set_rate()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", 1000);
        using var client = await UserClientAsync(factory, "Nino", Role.Manager);

        var rename = await client.PutAsJsonAsync($"/api/groups/{id}/name", new { name = "Pool" });
        var rate = await client.PutAsJsonAsync($"/api/groups/{id}/rate", new { hourlyRate = 12m });
        var changed = (await rate.Content.ReadFromJsonAsync<GroupDto>())!;

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(new GroupDto(id, "Pool", 12m, false), changed);
    }

    [Fact]
    public async Task Support_can_rename_and_set_rate()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", 1000);
        using var client = await SupportClientAsync(factory);

        var rename = await client.PutAsJsonAsync($"/api/groups/{id}/name", new { name = "Pool" });
        var rate = await client.PutAsJsonAsync($"/api/groups/{id}/rate", new { hourlyRate = 12m });
        var changed = (await rate.Content.ReadFromJsonAsync<GroupDto>())!;

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(new GroupDto(id, "Pool", 12m, false), changed);
    }

    [Fact]
    public async Task Manager_sees_only_active_groups()
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Billiards");
        await TestGroups.AddAsync(factory, "Old", archived: true);
        using var client = await UserClientAsync(factory, "Nino", Role.Manager);

        var groups = (await client.GetFromJsonAsync<List<GroupDto>>("/api/groups"))!;

        var names = groups.Select(g => g.Name).ToList();
        Assert.Equal(["Billiards"], names);
    }

    [Fact]
    public async Task Rate_is_sent_with_two_decimals()
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Billiards", 1000);
        await TestGroups.AddAsync(factory, "Pool", 1050);
        using var client = await UserClientAsync(factory, "Gio", Role.Staff);

        var json = await client.GetStringAsync("/api/groups");

        Assert.Contains("\"hourlyRate\":10.00", json);
        Assert.Contains("\"hourlyRate\":10.50", json);
    }

    [Fact]
    public async Task Manager_gets_403_on_create_and_archive()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        using var client = await UserClientAsync(factory, "Nino", Role.Manager);

        var create = await client.PostAsJsonAsync("/api/groups", new { name = "Pool", hourlyRate = 10m });
        var archive = await client.PostAsync($"/api/groups/{id}/archive", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task Staff_gets_403_on_every_change()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        using var client = await UserClientAsync(factory, "Gio", Role.Staff);

        var create = await client.PostAsJsonAsync("/api/groups", new { name = "Pool", hourlyRate = 10m });
        var rename = await client.PutAsJsonAsync($"/api/groups/{id}/name", new { name = "Pool" });
        var rate = await client.PutAsJsonAsync($"/api/groups/{id}/rate", new { hourlyRate = 12m });
        var archive = await client.PostAsync($"/api/groups/{id}/archive", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
    }

    [Fact]
    public async Task Staff_sees_only_active_groups()
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Billiards");
        await TestGroups.AddAsync(factory, "Old", archived: true);
        using var client = await UserClientAsync(factory, "Gio", Role.Staff);

        var groups = (await client.GetFromJsonAsync<List<GroupDto>>("/api/groups"))!;

        var names = groups.Select(g => g.Name).ToList();
        Assert.Equal(["Billiards"], names);
    }

    [Fact]
    public async Task Support_sees_archived_groups_too()
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Billiards");
        await TestGroups.AddAsync(factory, "Old", archived: true);
        using var client = await SupportClientAsync(factory);

        var groups = (await client.GetFromJsonAsync<List<GroupDto>>("/api/groups"))!;

        var names = groups.Select(g => g.Name).ToList();
        Assert.Equal(["Billiards", "Old"], names);
    }

    [Fact]
    public async Task Errors_have_codes_and_statuses()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        await TestSpotsInGroupAsync(factory, id);
        using var client = await SupportClientAsync(factory);

        var duplicate = await client.PostAsJsonAsync("/api/groups", new { name = "BILLIARDS", hourlyRate = 10m });
        var badRate = await client.PostAsJsonAsync("/api/groups", new { name = "Pool", hourlyRate = 0m });
        var hasSpots = await client.PostAsync($"/api/groups/{id}/archive", content: null);
        var missing = await client.PostAsync("/api/groups/99/archive", content: null);

        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, GroupError.DuplicateName);
        await AssertProblemAsync(badRate, HttpStatusCode.BadRequest, GroupError.InvalidRate);
        await AssertProblemAsync(hasSpots, HttpStatusCode.Conflict, GroupError.HasSpots);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, GroupError.NotFound);
    }


    private static Task<int> TestSpotsInGroupAsync(VenueAppFactory factory, int groupId)
    {
        return Spots.TestSpots.AddAsync(factory, "Billiard 1", null, null, groupId: groupId);
    }

    private static async Task<HttpClient> SupportClientAsync(VenueAppFactory factory)
    {
        await TestUsers.AddAsync(factory, "219volts Support", Role.Support, SupportPassword);
        var client = factory.CreateClient();
        var login = await client.SupportLoginAsync(SupportPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task<HttpClient> UserClientAsync(VenueAppFactory factory, string name, Role role)
    {
        var id = await TestUsers.AddAsync(factory, name, role, "1234");
        var client = factory.CreateClient();
        var login = await client.LoginAsync(id, "1234");
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
