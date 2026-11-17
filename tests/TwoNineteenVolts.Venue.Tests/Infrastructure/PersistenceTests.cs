using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Groups;
using TwoNineteenVolts.Venue.Tests.Spots;

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
    public async Task Device_and_spot_round_trip()
    {
        using var factory = new VenueAppFactory();
        var device = new Device("Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16, CreatedAt);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            db.Devices.Add(device);
            await db.SaveChangesAsync();
        }

        var group = await TestGroups.DefaultAsync(factory);
        var spot = new Spot("k7m2qx", "Billiard 1", group, device.Id, 5, CreatedAt);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            db.Spots.Add(spot);
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
            var loadedDevice = await db.Devices.SingleAsync(d => d.Id == device.Id);
            Assert.Equal("Panel 1", loadedDevice.Name);
            Assert.Equal(DeviceKind.WaveshareModbusTcp, loadedDevice.Kind);
            Assert.Equal("192.168.10.21", loadedDevice.IpAddress);
            Assert.Equal(502, loadedDevice.Port);
            Assert.Equal(16, loadedDevice.ChannelCount);
            Assert.Equal(CreatedAt, loadedDevice.CreatedAt);

            var loadedSpot = await db.Spots.SingleAsync(s => s.Id == spot.Id);
            Assert.Equal("k7m2qx", loadedSpot.Code);
            Assert.Equal("Billiard 1", loadedSpot.Name);
            Assert.Equal(device.Id, loadedSpot.DeviceId);
            Assert.Equal(5, loadedSpot.Channel);
            Assert.True(loadedSpot.IsMapped);
            Assert.False(loadedSpot.IsArchived);
            Assert.Equal(CreatedAt, loadedSpot.CreatedAt);
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

    [Fact]
    public async Task Session_and_charges_round_trip()
    {
        using var factory = new VenueAppFactory();
        var spotId = await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var endsAt = CreatedAt.AddMinutes(60);
        var endedAt = CreatedAt.AddMinutes(30);
        var session = new Session(spotId, CreatedAt, endsAt, 7, "Gio");
        session.Extend(15);
        session.End(endedAt, SessionEndReason.ManualOff, 8, "Nino");
        await SaveAsync(factory, session);
        var charge = new SessionCharge
        {
            SessionId = session.Id,
            Kind = ChargeKind.Extend,
            At = CreatedAt,
            UserId = null,
            UserName = "Timer",
            Minutes = 15,
            ComputedTetri = 250,
            ChargedTetri = 0
        };
        await SaveAsync(factory, charge);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var loaded = await db.Sessions.SingleAsync(s => s.Id == session.Id);
        var loadedCharge = await db.SessionCharges.SingleAsync(c => c.Id == charge.Id);
        Assert.Equal(spotId, loaded.SpotId);
        Assert.Equal(CreatedAt, loaded.StartedAt);
        Assert.Equal(endsAt.AddMinutes(15), loaded.EndsAt);
        Assert.Equal(endedAt, loaded.EndedAt);
        Assert.Equal(SessionEndReason.ManualOff, loaded.EndReason);
        Assert.Equal(7, loaded.StartedByUserId);
        Assert.Equal("Gio", loaded.StartedByName);
        Assert.Equal(8, loaded.EndedByUserId);
        Assert.Equal("Nino", loaded.EndedByName);
        Assert.False(loaded.IsRunning);
        Assert.Equal(session.Id, loadedCharge.SessionId);
        Assert.Equal(ChargeKind.Extend, loadedCharge.Kind);
        Assert.Null(loadedCharge.UserId);
        Assert.Equal("Timer", loadedCharge.UserName);
        Assert.Equal(15, loadedCharge.Minutes);
        Assert.Equal(250, loadedCharge.ComputedTetri);
        Assert.Equal(0, loadedCharge.ChargedTetri);
    }

    [Fact]
    public async Task Second_running_session_on_a_spot_is_refused_by_the_database()
    {
        using var factory = new VenueAppFactory();
        var spotId = await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var endsAt = CreatedAt.AddMinutes(60);
        await SaveAsync(factory, new Session(spotId, CreatedAt, endsAt, 7, "Gio"));

        var second = SaveAsync(factory, new Session(spotId, CreatedAt, endsAt, 7, "Gio"));

        await Assert.ThrowsAsync<DbUpdateException>(() => second);
    }

    [Fact]
    public async Task Ended_sessions_do_not_block_a_new_one()
    {
        using var factory = new VenueAppFactory();
        var spotId = await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var endsAt = CreatedAt.AddMinutes(60);
        var first = new Session(spotId, CreatedAt, endsAt, 7, "Gio");
        first.End(endsAt, SessionEndReason.TimeUp, null, "Timer");
        var second = new Session(spotId, CreatedAt, endsAt, 7, "Gio");
        second.End(endsAt, SessionEndReason.Stopped, 7, "Gio");
        await SaveAsync(factory, first);
        await SaveAsync(factory, second);

        await SaveAsync(factory, new Session(spotId, endsAt, endsAt.AddMinutes(30), 7, "Gio"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var count = await db.Sessions.CountAsync();
        Assert.Equal(3, count);
    }


    private static async Task SaveAsync<T>(VenueAppFactory factory, T entity) where T : class
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        db.Add(entity);
        await db.SaveChangesAsync();
    }
}
