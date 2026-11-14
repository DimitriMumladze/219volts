using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Infrastructure;

public class PersistenceTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task User_round_trips()
    {
        using var factory = new VenueAppFactory();
        var user = new User("Nino", Role.Manager, "secret-hash", CreatedAt);
        user.Deactivate();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            var loaded = await db.Users.SingleAsync(u => u.Id == user.Id);

            Assert.Equal("Nino", loaded.DisplayName);
            Assert.Equal(Role.Manager, loaded.Role);
            Assert.Equal("secret-hash", loaded.SecretHash);
            Assert.False(loaded.IsActive);
            Assert.Equal(CreatedAt, loaded.CreatedAt);
        }
    }

    [Fact]
    public async Task Dates_are_read_back_as_utc()
    {
        using var factory = new VenueAppFactory();
        var user = new User("Nino", Role.Staff, "hash", CreatedAt);
        var entry = new AuditEntry
        {
            OccurredAt = CreatedAt,
            ActorUserId = null,
            ActorName = "setup",
            Action = AuditActions.UserCreated,
            Target = null,
            Details = null
        };

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            db.Users.Add(user);
            db.AuditEntries.Add(entry);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            var loadedUser = await db.Users.SingleAsync(u => u.Id == user.Id);
            var loadedEntry = await db.AuditEntries.SingleAsync(e => e.Id == entry.Id);

            Assert.Equal(DateTimeKind.Utc, loadedUser.CreatedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, loadedEntry.OccurredAt.Kind);
        }
    }

    [Fact]
    public async Task Duplicate_name_differing_only_in_case_is_rejected_by_database()
    {
        using var factory = new VenueAppFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        db.Users.Add(new User("Nino", Role.Staff, "hash", CreatedAt));
        await db.SaveChangesAsync();

        db.Users.Add(new User("NINO", Role.Staff, "hash", CreatedAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_entry_round_trips()
    {
        using var factory = new VenueAppFactory();
        var entry = new AuditEntry
        {
            OccurredAt = CreatedAt,
            ActorUserId = null,
            ActorName = "unknown",
            Action = AuditActions.LoginFailed,
            Target = "user:7",
            Details = null
        };

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            db.AuditEntries.Add(entry);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            var loaded = await db.AuditEntries.SingleAsync(e => e.Id == entry.Id);

            Assert.Equal(CreatedAt, loaded.OccurredAt);
            Assert.Null(loaded.ActorUserId);
            Assert.Equal("unknown", loaded.ActorName);
            Assert.Equal(AuditActions.LoginFailed, loaded.Action);
            Assert.Equal("user:7", loaded.Target);
            Assert.Null(loaded.Details);
        }
    }
}
