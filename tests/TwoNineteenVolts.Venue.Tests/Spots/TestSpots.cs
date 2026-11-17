using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Groups;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public static class TestSpots
{
    public static async Task<int> AddAsync(VenueAppFactory factory, string name, int? deviceId, int? channel, bool isArchived = false, int? groupId = null)
    {
        var group = groupId ?? await TestGroups.DefaultAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        var codes = new SpotCodeGenerator();
        var code = codes.NewCode();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var spot = new Spot(code, name, group, deviceId, channel, now);
        if (isArchived)
        {
            spot.Archive();
        }

        db.Spots.Add(spot);
        await db.SaveChangesAsync();
        return spot.Id;
    }
}
