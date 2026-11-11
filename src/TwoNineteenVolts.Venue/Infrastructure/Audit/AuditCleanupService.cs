using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Audit;

// Deletes audit entries older than the retention period: once at start, then once a day.
public class AuditCleanupService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<AuditCleanupService> logger) : BackgroundService
{
    public const int RetentionDays = 365;

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

    public async Task RunOnceAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);
        var deleted = await db.AuditEntries
            .Where(e => e.OccurredAt < cutoff)
            .ExecuteDeleteAsync();

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} audit entries older than {RetentionDays} days", deleted, RetentionDays);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Also runs at start: a venue PC that is switched off every night would never reach a 24-hour tick.
        await RunSafelyAsync();

        using var timer = new PeriodicTimer(CleanupInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync();
        }
    }

    // An exception escaping a background service stops the whole app; one failed run must not.
    private async Task RunSafelyAsync()
    {
        try
        {
            await RunOnceAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Audit cleanup run failed");
        }
    }
}
