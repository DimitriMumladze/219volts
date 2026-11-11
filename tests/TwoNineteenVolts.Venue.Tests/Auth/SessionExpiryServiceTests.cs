using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class SessionExpiryServiceTests
{
    [Fact]
    public async Task Expired_session_is_removed_and_audited()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = factory.CreateClient();
        await client.LoginAsync(nino, "1234");

        factory.Time.Advance(TimeSpan.FromMinutes(61));
        await RunOnceAsync(factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var expired = await SingleEntryAsync(factory, AuditActions.SessionExpired);
        Assert.Equal(nino, expired.ActorUserId);
        Assert.Equal("Nino", expired.ActorName);
        Assert.Equal($"user:{nino}", expired.Target);
    }

    [Fact]
    public async Task Session_used_within_timeout_is_kept()
    {
        using var factory = new VenueAppFactory();
        var nino = await TestUsers.AddAsync(factory, "Nino", Role.Manager, "1234");
        using var client = factory.CreateClient();
        await client.LoginAsync(nino, "1234");

        factory.Time.Advance(TimeSpan.FromMinutes(35));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        factory.Time.Advance(TimeSpan.FromMinutes(30));
        await RunOnceAsync(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        Assert.False(await db.AuditEntries.AnyAsync(e => e.Action == AuditActions.SessionExpired));
    }

    private static Task RunOnceAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<SessionExpiryService>(factory.Services).RunOnceAsync();

    private static async Task<AuditEntry> SingleEntryAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }
}
