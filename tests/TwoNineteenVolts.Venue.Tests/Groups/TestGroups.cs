using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Groups;

public static class TestGroups
{
    public const string DefaultName = "Test group";

    public static async Task<int> AddAsync(VenueAppFactory factory, string name, long hourlyRateTetri = 1000, bool archived = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        var now = factory.Time.GetUtcNow().UtcDateTime;
        var group = new Group(name, hourlyRateTetri, now);
        if (archived)
        {
            group.Archive();
        }

        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    public static async Task<int> DefaultAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var existing = await db.Groups.SingleOrDefaultAsync(g => g.Name == DefaultName);
        if (existing is not null)
        {
            return existing.Id;
        }

        return await AddAsync(factory, DefaultName);
    }
}
