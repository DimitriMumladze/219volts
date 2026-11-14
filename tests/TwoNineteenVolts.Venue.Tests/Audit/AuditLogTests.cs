using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Audit;

public class AuditLogTests
{
    private static readonly AuditActor Manager = new(1, "Nino");

    [Fact]
    public async Task Entry_is_saved_together_with_the_change()
    {
        using var factory = new VenueAppFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();

            db.Users.Add(new User("Gio", Role.Staff, "hash", factory.Time.GetUtcNow().UtcDateTime));
            audit.Record(Manager, AuditActions.UserCreated, "user:2", "Staff");
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

            Assert.Equal("Gio", (await db.Users.SingleAsync()).DisplayName);

            var entry = await db.AuditEntries.SingleAsync();
            Assert.Equal(factory.Time.GetUtcNow().UtcDateTime, entry.OccurredAt);
            Assert.Equal(1, entry.ActorUserId);
            Assert.Equal("Nino", entry.ActorName);
            Assert.Equal(AuditActions.UserCreated, entry.Action);
            Assert.Equal("user:2", entry.Target);
            Assert.Equal("Staff", entry.Details);
        }
    }

    [Fact]
    public async Task Entry_is_not_saved_without_SaveChanges()
    {
        using var factory = new VenueAppFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
            audit.Record(AuditActor.Unknown, AuditActions.LoginFailed, "user:7");
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

            Assert.Equal(0, await db.AuditEntries.CountAsync());
        }
    }

    [Fact]
    public void Unknown_actor_has_no_user_id_and_is_named_unknown()
    {
        Assert.Equal(new AuditActor(null, "unknown"), AuditActor.Unknown);
    }
}
