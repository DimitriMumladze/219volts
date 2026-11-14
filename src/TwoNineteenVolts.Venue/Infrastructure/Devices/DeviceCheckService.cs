using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class DeviceCheckService(
    IServiceScopeFactory scopes,
    DeviceStatuses statuses,
    IOptions<DevicesOptions> options,
    TimeProvider time,
    ILogger<DeviceCheckService> logger) : BackgroundService
{
    public async Task RunOnceAsync()
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var drivers = scope.ServiceProvider.GetRequiredService<RelayDrivers>();

        var devices = await db.Devices.AsNoTracking().ToListAsync();

        var checks = devices.Select(device => CheckAsync(device, drivers)).ToList();
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


    private async Task CheckAsync(Device device, RelayDrivers drivers)
    {
        var driver = drivers.For(device.Kind);
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        var result = await driver.ReadAllAsync(device, timeout.Token);

        statuses.RecordOutcome(device.Id, result.Error);
        if (!result.IsFailure)
        {
            statuses.StoreChannels(device.Id, result.Value);
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
