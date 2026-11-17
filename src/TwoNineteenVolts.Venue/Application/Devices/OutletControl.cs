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

    public async Task<Result> SwitchAsync(bool shouldBeOn)
    {
        var set = await RelayCall.RunAsync(device, statuses, time, token => driver.SetAsync(device, channel, shouldBeOn, token));
        if (set.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return set;
        }

        var readBack = await RelayCall.RunAsync(device, statuses, time, token => driver.ReadIsOnAsync(device, channel, token));
        if (readBack.IsFailure)
        {
            statuses.ForgetChannel(device.Id, channel);
            return Result.Fail(readBack.Error);
        }

        var isOn = readBack.Value;
        statuses.StoreChannel(device.Id, device.ChannelCount, channel, isOn);
        if (isOn != shouldBeOn)
        {
            return Result.Fail(DeviceError.DidNotObey);
        }

        return Result.Ok();
    }

    public void ForgetState()
    {
        statuses.ForgetChannel(device.Id, channel);
    }
}
