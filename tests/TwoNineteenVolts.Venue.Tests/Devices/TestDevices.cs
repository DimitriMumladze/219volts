using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Devices;

// Inserts devices straight into the database, so tests don't depend on device management.
public static class TestDevices
{
    public static async Task<int> AddAsync(
        VenueAppFactory factory,
        string name,
        DeviceKind kind,
        string ipAddress,
        int port,
        int channelCount)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        var device = new Device(name, kind, ipAddress, port, channelCount, factory.Time.GetUtcNow().UtcDateTime);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }
}
