using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public static class RelayCall
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<Result<T>> RunAsync<T>(
        Device device,
        DeviceStatuses statuses,
        TimeProvider time,
        Func<CancellationToken, Task<Result<T>>> call)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        var result = await call(timeout.Token);
        statuses.RecordOutcome(device.Id, result.Error);
        return result;
    }
}
