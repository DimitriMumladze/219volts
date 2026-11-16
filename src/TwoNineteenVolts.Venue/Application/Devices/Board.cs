using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class Board(Device device, IRelayDriver driver, DeviceStatuses statuses, TimeProvider time)
{
    public async Task<Result<bool>> SwitchAsync(int channel, bool on)
    {
        var set = await CallAsync(token => driver.SetAsync(device, channel, on, token));
        if (set.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return set;
        }

        var read = await CallAsync(token => driver.ReadAsync(device, channel, token));
        if (read.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return read;
        }

        statuses.StoreChannel(device.Id, device.ChannelCount, channel, read.Value);
        return read;
    }

    public async Task<Result<IReadOnlyList<bool>>> ReadAllAsync()
    {
        var result = await CallAsync(token => driver.ReadAllAsync(device, token));
        if (!result.IsFailure)
        {
            statuses.StoreChannels(device.Id, result.Value);
        }

        return result;
    }


    private async Task<Result<T>> CallAsync<T>(Func<CancellationToken, Task<Result<T>>> call)
    {
        using var timeout = new CancellationTokenSource(RelayCall.Timeout, time);
        var result = await call(timeout.Token);
        statuses.RecordOutcome(device.Id, result.Error);
        return result;
    }
}
