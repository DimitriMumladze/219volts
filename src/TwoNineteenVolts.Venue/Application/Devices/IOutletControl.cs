using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Devices;

public interface IOutletControl
{
    OutletState State { get; }

    Task<Result<bool>> SwitchAsync(bool on);

    void ForgetState();
}
