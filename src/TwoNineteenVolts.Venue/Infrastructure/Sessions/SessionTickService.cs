using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Sessions;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public class SessionTickService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<SessionTickService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    public async Task RunStartupAsync()
    {
        using var scope = scopes.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        await sessions.EndDueAsync(AuditActor.Restart);
    }

    public async Task RunOnceAsync()
    {
        using var scope = scopes.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        await sessions.EndDueAsync(AuditActor.Timer);
        await sessions.RestoreDriftedAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync(RunStartupAsync);

        using var timer = new PeriodicTimer(TickInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(RunOnceAsync);
        }
    }


    private async Task RunSafelyAsync(Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Session tick failed");
        }
    }
}
