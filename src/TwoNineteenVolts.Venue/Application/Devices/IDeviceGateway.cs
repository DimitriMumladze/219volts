using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Devices;

public interface IDeviceGateway
{
    Task<Result<bool>> SwitchAsync(Outlet outlet, bool on);

    Task<OutletState> StateOfAsync(Outlet outlet);

    Task<IReadOnlyDictionary<Outlet, OutletState>> StatesOfAsync(IReadOnlyCollection<Outlet> outlets);

    Task<Result<T>> WithOutletAsync<T>(Outlet outlet, Func<IOutletControl, Task<Result<T>>> steps);

    Task<OutletCheck> CheckOutletAsync(Outlet outlet);
}
