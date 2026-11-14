using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Audit;

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
        await RunSafelyAsync();

        using var timer = new PeriodicTimer(CleanupInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync();
        }
    }


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
