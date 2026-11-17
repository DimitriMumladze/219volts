using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Sessions;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public class SessionTickService(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<SessionTickService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    public Task RunStartupAsync(CancellationToken cancellationToken = default)
    {
        return EndDueAsync(AuditActor.Restart, cancellationToken);
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await EndDueAsync(AuditActor.Timer, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        await sessions.RestoreDriftedAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync(() => RunStartupAsync(stoppingToken));

        using var timer = new PeriodicTimer(TickInterval, time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(() => RunOnceAsync(stoppingToken));
        }
    }


    private async Task EndDueAsync(AuditActor actor, CancellationToken cancellationToken)
    {
        List<int> deviceIds;
        using (var scope = scopes.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
            deviceIds = await sessions.FindDeviceIdsWithDueSessionsAsync();
        }

        var perDevice = deviceIds.Select(id => EndDueOnDeviceAsync(id, actor, cancellationToken)).ToList();
        await Task.WhenAll(perDevice);
    }

    private async Task EndDueOnDeviceAsync(int deviceId, AuditActor actor, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
        await sessions.EndDueOnDeviceAsync(deviceId, actor, cancellationToken);
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
