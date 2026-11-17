using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class DeviceGateway(
    IVenueDbContext db,
    DeviceLocks locks,
    RelayDrivers drivers,
    DeviceStatuses statuses,
    TimeProvider time) : IDeviceGateway
{
    private static readonly AsyncLocal<bool> Holding = new();

    public Task<Result<bool>> SwitchAsync(Outlet outlet, bool on)
    {
        return WithOutletAsync(outlet, control => control.SwitchAsync(on));
    }

    public async Task<OutletState> StateOfAsync(Outlet outlet)
    {
        List<Outlet> outlets = [outlet];
        var states = await StatesOfAsync(outlets);
        return states.GetValueOrDefault(outlet, OutletState.Unknown);
    }

    public async Task<IReadOnlyDictionary<Outlet, OutletState>> StatesOfAsync(IReadOnlyCollection<Outlet> outlets)
    {
        if (outlets.Count == 0)
        {
            return new Dictionary<Outlet, OutletState>();
        }

        var deviceIds = outlets.Select(o => o.DeviceId).Distinct().ToList();
        var names = await db.Devices
            .AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name);

        var states = new Dictionary<Outlet, OutletState>();
        foreach (var outlet in outlets)
        {
            if (names.TryGetValue(outlet.DeviceId, out var name))
            {
                states[outlet] = statuses.StateOf(outlet, name);
            }
        }

        return states;
    }

    public async Task<Result<T>> WithOutletAsync<T>(Outlet outlet, Func<IOutletControl, Task<Result<T>>> steps)
    {
        ThrowIfHolding();
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == outlet.DeviceId);
        if (device is null)
        {
            return Result<T>.Fail(DeviceError.NotFound);
        }

        return await HoldAsync(device, driver =>
        {
            var control = new OutletControl(device, outlet.Channel, driver, statuses, time);
            return steps(control);
        });
    }

    public async Task<OutletCheck> CheckOutletAsync(Outlet outlet)
    {
        var device = await db.Devices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == outlet.DeviceId);
        if (device is null)
        {
            return OutletCheck.UnknownDevice;
        }

        if (outlet.Channel < 1 || outlet.Channel > device.ChannelCount)
        {
            return OutletCheck.ChannelOutOfRange;
        }

        return OutletCheck.Fine;
    }

    public Task<Result<IReadOnlyList<bool>>> CheckDeviceAsync(Device device)
    {
        return HoldAsync(device, driver => ReadAllAsync(device, driver));
    }


    private async Task<Result<T>> HoldAsync<T>(Device device, Func<IRelayDriver, Task<Result<T>>> work)
    {
        ThrowIfHolding();
        var deviceLock = locks.For(device.Id);
        var entered = await EnterAsync(deviceLock);
        if (!entered)
        {
            statuses.RecordOutcome(device.Id, DeviceError.NoAnswer);
            return Result<T>.Fail(DeviceError.NoAnswer);
        }

        Holding.Value = true;
        try
        {
            var driver = drivers.For(device.Kind);
            return await work(driver);
        }
        finally
        {
            Holding.Value = false;
            deviceLock.Release();
        }
    }

    private async Task<bool> EnterAsync(SemaphoreSlim deviceLock)
    {
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        try
        {
            await deviceLock.WaitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, IRelayDriver driver)
    {
        var result = await RelayCall.RunAsync(device, statuses, time, token => driver.ReadAllAsync(device, token));
        if (!result.IsFailure)
        {
            statuses.StoreChannels(device.Id, result.Value);
        }

        return result;
    }

    private static void ThrowIfHolding()
    {
        if (Holding.Value)
        {
            throw new InvalidOperationException("A device is already held in this flow; never hold two at once.");
        }
    }
}
