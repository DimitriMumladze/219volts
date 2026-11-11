using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace TwoNineteenVolts.Venue.Tests;

public class VenueAppFactory : WebApplicationFactory<Program>
{
    public string DataFolder { get; } = Path.Combine(Path.GetTempPath(), "219volts-tests", Guid.NewGuid().ToString("N"));

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Venue:DataFolder", DataFolder);
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Time));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(DataFolder))
        {
            // SQLite keeps closed connections in a pool, which keeps venue.db locked on Windows.
            SqliteConnection.ClearAllPools();
            Directory.Delete(DataFolder, recursive: true);
        }
    }
}
