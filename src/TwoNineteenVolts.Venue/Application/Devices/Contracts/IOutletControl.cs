using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Devices.Contracts;

public interface IOutletControl
{
    OutletState State { get; }

    Task<Result> SwitchAsync(bool shouldBeOn);

    void ForgetState();
}
