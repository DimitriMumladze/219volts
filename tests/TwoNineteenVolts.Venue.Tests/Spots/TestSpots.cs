using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public static class TestSpots
{
    public static async Task<int> AddAsync(VenueAppFactory factory, string name, int? deviceId, int? channel, bool archived = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        var codes = new SpotCodeGenerator();
        var code = codes.NewCode();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var spot = new Spot(code, name, deviceId, channel, now);
        if (archived)
        {
            spot.Archive();
        }

        db.Spots.Add(spot);
        await db.SaveChangesAsync();
        return spot.Id;
    }
}
