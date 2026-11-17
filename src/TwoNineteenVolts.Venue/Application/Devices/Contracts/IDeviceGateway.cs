using TwoNineteenVolts.Venue.Common;

namespace TwoNineteenVolts.Venue.Application.Devices.Contracts;

public interface IDeviceGateway
{
    Task<Result> SwitchAsync(Outlet outlet, bool shouldBeOn);

    Task<OutletState> StateOfAsync(Outlet outlet);

    Task<IReadOnlyDictionary<Outlet, OutletState>> StatesOfAsync(IReadOnlyCollection<Outlet> outlets);

    Task<Result> WithOutletAsync(Outlet outlet, Func<IOutletControl, Task<Result>> steps);

    Task<Result<T>> WithOutletAsync<T>(Outlet outlet, Func<IOutletControl, Task<Result<T>>> steps);

    Task<OutletCheck> CheckOutletAsync(Outlet outlet);
}
