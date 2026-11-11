using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Tests.Audit;

// Writes audit entries with chosen times straight into the database.
public static class AuditTestData
{
    public static AuditEntry Entry(DateTime occurredAt, int? actorUserId, string action, string details) =>
        new()
        {
            OccurredAt = occurredAt,
            ActorUserId = actorUserId,
            ActorName = actorUserId is null ? "unknown" : $"user {actorUserId}",
            Source = AuditSource.Local,
            Action = action,
            Target = null,
            Details = details
        };

    public static async Task AddAsync(VenueAppFactory factory, params AuditEntry[] entries)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        db.AuditEntries.AddRange(entries);
        await db.SaveChangesAsync();
    }
}
