using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public static class RelayCall
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<Result> RunAsync(
        Device device,
        DeviceStatuses statuses,
        TimeProvider time,
        Func<CancellationToken, Task<Result>> call)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        var result = await call(timeout.Token);
        var hasAnswered = result.Error != DeviceError.NoAnswer;
        RecordReachability(device, statuses, hasAnswered);
        return result;
    }

    public static async Task<Result<T>> RunAsync<T>(
        Device device,
        DeviceStatuses statuses,
        TimeProvider time,
        Func<CancellationToken, Task<Result<T>>> call)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        var result = await call(timeout.Token);
        var hasAnswered = result.Error != DeviceError.NoAnswer;
        RecordReachability(device, statuses, hasAnswered);
        return result;
    }


    private static void RecordReachability(Device device, DeviceStatuses statuses, bool hasAnswered)
    {
        if (hasAnswered)
        {
            statuses.RecordAnswer(device.Id);
        }
        else
        {
            statuses.RecordMiss(device.Id);
        }
    }
}
