using System.Net;

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
        Assert.True(File.Exists(Path.Combine(factory.DataFolder, "venue.db")));
    }
}
