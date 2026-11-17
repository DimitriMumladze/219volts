using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class SwitchFailuresTests
{
    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);

    [Fact]
    public async Task Vanished_device_is_reported_as_unmapped_without_an_audit_entry()
    {
        using var factory = new VenueAppFactory();
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", null, null);

        var reported = await RecordAsync(factory, spot, DeviceError.NotFound);

        var auditCount = await AuditCountAsync(factory);
        Assert.Equal(SpotError.Unmapped, reported);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task Real_switch_failures_are_reported_as_they_are_and_audited()
    {
        using var factory = new VenueAppFactory();
        var spot = await TestSpots.AddAsync(factory, "Billiard 1", null, null);

        var reported = await RecordAsync(factory, spot, DeviceError.NoAnswer);

        var auditCount = await AuditCountAsync(factory);
        Assert.Equal(DeviceError.NoAnswer, reported);
        Assert.Equal(1, auditCount);
    }


    private static async Task<string> RecordAsync(VenueAppFactory factory, int spot, string error)
    {
        using var scope = factory.Services.CreateScope();
        var failures = scope.ServiceProvider.GetRequiredService<SwitchFailures>();
        return await failures.RecordAsync(spot, Staff, error);
    }

    private static async Task<int> AuditCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync();
    }
}
