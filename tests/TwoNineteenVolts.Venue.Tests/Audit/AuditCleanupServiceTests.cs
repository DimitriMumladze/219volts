using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Audit;

public class AuditCleanupServiceTests
{
    [Fact]
    public async Task Cleanup_deletes_only_entries_older_than_365_days()
    {
        using var factory = new VenueAppFactory();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await AuditTestData.AddAsync(
            factory,
            AuditTestData.Entry(now.AddDays(-366), 1, AuditActions.Login, "too old"),
            AuditTestData.Entry(now.AddDays(-365).AddMinutes(-1), 1, AuditActions.Login, "just too old"),
            AuditTestData.Entry(now.AddDays(-365).AddMinutes(1), 1, AuditActions.Login, "just kept"),
            AuditTestData.Entry(now.AddDays(-1), 1, AuditActions.Login, "recent"));

        await ActivatorUtilities.CreateInstance<AuditCleanupService>(factory.Services).RunOnceAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var kept = await db.AuditEntries.OrderBy(e => e.OccurredAt).Select(e => e.Details).ToListAsync();
        Assert.Equal(["just kept", "recent"], kept);
    }
}
