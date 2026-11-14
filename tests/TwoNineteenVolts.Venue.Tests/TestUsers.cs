using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests;

public static class TestUsers
{
    public static async Task<int> AddAsync(VenueAppFactory factory, string name, Role role, string secret, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        var secretHash = hasher.Hash(secret);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var user = new User(name, role, secretHash, now);
        if (!isActive)
        {
            user.Deactivate();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
