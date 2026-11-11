using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace TwoNineteenVolts.Venue.Tests;

public class VenueAppFactory : WebApplicationFactory<Program>
{
    public string DataFolder { get; } = Path.Combine(Path.GetTempPath(), "219volts-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Venue:DataFolder", DataFolder);
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
