using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// Every minute: ends sessions idle longer than Auth:InactivityTimeout and records each one.
public class SessionExpiryService(
    ISessionRegistry sessions,
    IServiceScopeFactory scopes,
    IOptions<AuthOptions> options,
    TimeProvider time,
    ILogger<SessionExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    public async Task RunOnceAsync()
    {
        var expired = sessions.RemoveIdleSessions(options.Value.InactivityTimeout);
        if (expired.Count == 0)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();

        foreach (var session in expired)
        {
            audit.Record(new AuditActor(session.UserId, session.UserName, AuditSource.Local), AuditActions.SessionExpired, $"user:{session.UserId}");
        }

        await db.SaveChangesAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // An exception escaping a background service stops the whole app; one failed run must not.
            try
            {
                await RunOnceAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Session expiry run failed");
            }
        }
    }
}
