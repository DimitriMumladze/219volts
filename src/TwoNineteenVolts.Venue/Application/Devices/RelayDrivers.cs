using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class RelayDrivers(IEnumerable<IRelayDriver> drivers)
{
    public IRelayDriver For(DeviceKind kind) => drivers.Single(d => d.Kind == kind);
}
