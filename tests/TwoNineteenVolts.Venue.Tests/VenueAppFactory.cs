using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Tests.Devices;

namespace TwoNineteenVolts.Venue.Tests;

public class VenueAppFactory : WebApplicationFactory<Program>
{
    public string DataFolder { get; } = Path.Combine(Path.GetTempPath(), "219volts-tests", Guid.NewGuid().ToString("N"));

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));

    public FakeRelayDriver Shelly { get; } = new(DeviceKind.ShellyGen2);

    public FakeRelayDriver Waveshare { get; } = new(DeviceKind.WaveshareModbusTcp);

    public Action<IServiceCollection>? ExtraServices { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Venue:DataFolder", DataFolder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);

            var backgroundServices = services
                .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType?.Assembly == typeof(Program).Assembly)
                .ToList();
            foreach (var descriptor in backgroundServices)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IRelayDriver>();
            services.AddSingleton<IRelayDriver>(Shelly);
            services.AddSingleton<IRelayDriver>(Waveshare);
            ExtraServices?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(DataFolder))
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(DataFolder, recursive: true);
        }
    }
}
