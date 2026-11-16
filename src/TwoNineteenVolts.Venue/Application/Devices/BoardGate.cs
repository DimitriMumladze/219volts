using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class BoardGate(BoardGates gates, RelayDrivers drivers, DeviceStatuses statuses, TimeProvider time)
{
    private static readonly AsyncLocal<bool> InsideGate = new();

    public async Task<Result<T>> RunAsync<T>(Device device, Func<Board, Task<Result<T>>> work)
    {
        if (InsideGate.Value)
        {
            throw new InvalidOperationException("A board gate is already held in this flow; never hold two gates at once.");
        }

        var gate = gates.For(device.Id);
        var entered = await EnterAsync(gate);
        if (!entered)
        {
            statuses.RecordOutcome(device.Id, DeviceError.NoAnswer);
            return Result<T>.Fail(DeviceError.NoAnswer);
        }

        InsideGate.Value = true;
        try
        {
            var driver = drivers.For(device.Kind);
            var board = new Board(device, driver, statuses, time);
            return await work(board);
        }
        finally
        {
            InsideGate.Value = false;
            gate.Release();
        }
    }


    private async Task<bool> EnterAsync(SemaphoreSlim gate)
    {
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        try
        {
            await gate.WaitAsync(timeout.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
