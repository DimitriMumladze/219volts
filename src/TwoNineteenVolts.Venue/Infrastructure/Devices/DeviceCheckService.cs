using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class DeviceCheckService(
    IServiceScopeFactory scopes,
    IOptions<DevicesOptions> options,
    TimeProvider time,
    ILogger<DeviceCheckService> logger) : BackgroundService
{
    public async Task RunOnceAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var gate = scope.ServiceProvider.GetRequiredService<BoardGate>();

        var devices = await db.Devices.AsNoTracking().ToListAsync();

        var checks = devices.Select(device => gate.RunAsync(device, board => board.ReadAllAsync())).ToList();
        await Task.WhenAll(checks);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunSafelyAsync();

        using var timer = new PeriodicTimer(options.Value.CheckInterval, time);
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
            logger.LogError(exception, "Device check run failed");
        }
    }
}
