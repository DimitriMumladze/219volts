using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class OutletControl(Device device, int channel, IRelayDriver driver, DeviceStatuses statuses, TimeProvider time) : IOutletControl
{
    public OutletState State
    {
        get
        {
            var outlet = new Outlet(device.Id, channel);
            return statuses.StateOf(outlet, device.Name);
        }
    }

    public async Task<Result<bool>> SwitchAsync(bool on)
    {
        var set = await RelayCall.RunAsync(device, statuses, time, token => driver.SetAsync(device, channel, on, token));
        if (set.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return set;
        }

        var read = await RelayCall.RunAsync(device, statuses, time, token => driver.ReadAsync(device, channel, token));
        if (read.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return read;
        }

        statuses.StoreChannel(device.Id, device.ChannelCount, channel, read.Value);
        if (read.Value != on)
        {
            return Result<bool>.Fail(DeviceError.DidNotObey);
        }

        return read;
    }

    public void ForgetState()
    {
        statuses.ForgetChannel(device.Id, channel);
    }
}
