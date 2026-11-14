using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

// Picks the driver for a device's kind from all registered drivers.
public class RelayDrivers(IEnumerable<IRelayDriver> drivers)
{
    // Every kind has a driver; a missing one is a registration bug, not an expected failure.
    public IRelayDriver For(DeviceKind kind) => drivers.Single(d => d.Kind == kind);
}
