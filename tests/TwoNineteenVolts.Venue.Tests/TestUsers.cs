using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests;

// Inserts users straight into the database, so endpoint tests don't depend on user management.
public static class TestUsers
{
    public static async Task<int> AddAsync(VenueAppFactory factory, string name, Role role, string secret, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        var user = new User(name, role, hasher.Hash(secret), factory.Time.GetUtcNow().UtcDateTime);
        if (!isActive)
        {
            user.Deactivate();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}
