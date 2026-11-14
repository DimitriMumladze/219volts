using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Tests;

public class StartupTests
{
    [Fact]
    public async Task App_starts_and_creates_database_in_data_folder()
    {
        using var factory = new VenueAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var databasePath = Path.Combine(factory.DataFolder, "venue.db");
        var exists = File.Exists(databasePath);
        Assert.True(exists);
    }

    [Fact]
    public async Task Framework_errors_carry_a_code()
    {
        using var factory = new VenueAppFactory();
        var gio = await TestUsers.AddAsync(factory, "Gio", Role.Staff, "5678");
        using var anonymous = factory.CreateClient();
        using var staff = factory.CreateClient();
        await staff.LoginAsync(gio, "5678");

        var unauthorized = await anonymous.GetAsync("/api/users");
        var badRequest = await anonymous.PostAsJsonAsync("/api/auth/login", new { userId = gio });
        var forbidden = await staff.GetAsync("/api/users");
        var notFound = await staff.GetAsync("/api/nothing-here");

        await AssertCodeAsync(unauthorized, HttpStatusCode.Unauthorized, "Unauthorized");
        await AssertCodeAsync(badRequest, HttpStatusCode.BadRequest, "BadRequest");
        await AssertCodeAsync(forbidden, HttpStatusCode.Forbidden, "Forbidden");
        await AssertCodeAsync(notFound, HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public void Zero_check_interval_refuses_to_start()
    {
        using var factory = new VenueAppFactory();
        using var zeroInterval = factory.WithWebHostBuilder(builder => builder.UseSetting("Devices:CheckInterval", "00:00:00"));

        Assert.Throws<OptionsValidationException>(() => zeroInterval.CreateClient());
    }


    private static async Task AssertCodeAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
        var problemCode = problem.Extensions["code"]!.ToString();
        Assert.Equal(code, problemCode);
    }
}
