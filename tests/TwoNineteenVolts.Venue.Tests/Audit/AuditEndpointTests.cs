using System.Net;
using System.Net.Http.Json;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Tests.Audit;

public class AuditEndpointTests
{
    private static readonly DateTime Nov1 = new(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

    private record EntryDto(long Id, DateTime OccurredAt, int? ActorUserId, string ActorName, string Source, string Action, string? Target, string? Details);

    private record PageDto(List<EntryDto> Items, int Page, int PageSize, int TotalCount);

    [Fact]
    public async Task Audit_endpoint_filters_by_user_action_and_dates()
    {
        using var factory = new VenueAppFactory();
        using var client = await LoggedInManagerAsync(factory);
        await AuditTestData.AddAsync(
            factory,
            AuditTestData.Entry(Nov1, 7, AuditActions.UserRenamed, "e1"),
            AuditTestData.Entry(Nov1.AddDays(1), 7, AuditActions.UserPinSet, "e2"),
            AuditTestData.Entry(Nov1.AddDays(2), 8, AuditActions.UserRenamed, "e3"));

        Assert.Equal(["e2", "e1"], await DetailsAsync(client, "?userId=7"));
        Assert.Equal(["e3", "e1"], await DetailsAsync(client, "?action=user.renamed"));
        Assert.Equal(["e3", "e2"], await DetailsAsync(client, "?from=2026-11-02T08:00:00Z&to=2026-11-03T08:00:00Z"));
        Assert.Equal(["e3", "e2"], await DetailsAsync(client, "?from=2026-11-02T08:00:00&to=2026-11-03T08:00:00"));
        Assert.Equal(["e3", "e2"], await DetailsAsync(client, "?from=2026-11-02T12:00:00%2B04:00&to=2026-11-03T12:00:00%2B04:00"));
        Assert.Equal(["e3"], await DetailsAsync(client, "?userId=8&action=user.renamed&from=2026-11-01T00:00:00Z"));
    }

    [Fact]
    public async Task Audit_endpoint_returns_times_in_utc()
    {
        using var factory = new VenueAppFactory();
        using var client = await LoggedInManagerAsync(factory);
        await AuditTestData.AddAsync(factory, AuditTestData.Entry(Nov1, 7, AuditActions.UserRenamed, "e1"));

        var json = await client.GetStringAsync("/api/audit?userId=7");

        Assert.Contains("\"occurredAt\":\"2026-11-01T08:00:00Z\"", json);
    }

    [Fact]
    public async Task Audit_endpoint_pages_newest_first_and_caps_page_size_at_200()
    {
        using var factory = new VenueAppFactory();
        using var client = await LoggedInManagerAsync(factory);
        var entries = Enumerable.Range(0, 250)
            .Select(i => AuditTestData.Entry(Nov1.AddMinutes(i), 7, AuditActions.UserPinSet, $"e{i}"))
            .ToArray();
        await AuditTestData.AddAsync(factory, entries);

        var first = await client.GetFromJsonAsync<PageDto>("/api/audit?userId=7&pageSize=500");
        Assert.Equal((1, 200, 250), (first!.Page, first.PageSize, first.TotalCount));
        Assert.Equal(200, first.Items.Count);
        Assert.Equal("e249", first.Items[0].Details);
        Assert.Equal("e50", first.Items[^1].Details);

        var second = await client.GetFromJsonAsync<PageDto>("/api/audit?userId=7&page=2&pageSize=200");
        Assert.Equal(50, second!.Items.Count);
        Assert.Equal("e49", second.Items[0].Details);
        Assert.Equal("e0", second.Items[^1].Details);

        var defaults = await client.GetFromJsonAsync<PageDto>("/api/audit?userId=7");
        Assert.Equal((1, 50, 50), (defaults!.Page, defaults.PageSize, defaults.Items.Count));
    }

    [Fact]
    public async Task Staff_gets_403_on_audit()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var client = factory.CreateClient();
        await client.LoginAsync(gio, "5678");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/audit")).StatusCode);
    }

    private static async Task<HttpClient> LoggedInManagerAsync(VenueAppFactory factory)
    {
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(nino, "1234")).StatusCode);
        return client;
    }

    private static async Task<List<string?>> DetailsAsync(HttpClient client, string query)
    {
        var page = await client.GetFromJsonAsync<PageDto>("/api/audit" + query);
        return page!.Items.Select(e => e.Details).ToList();
    }
}
