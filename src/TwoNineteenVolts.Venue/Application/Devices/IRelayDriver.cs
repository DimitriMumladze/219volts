using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

// One implementation per device kind. A driver also owns the rules of its kind (default port,
// possible channel counts), so adding a kind never touches the services.
// Channels are numbered from 1 here; each driver converts to its own numbering.
// A failure is DeviceError.NoAnswer or DeviceError.ErrorAnswer, never an exception.
public interface IRelayDriver
{
    DeviceKind Kind { get; }

    int DefaultPort { get; }

    IReadOnlyList<int> AllowedChannelCounts { get; }

    // States of all channels, index 0 = channel 1.
    Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken);

    Task<Result<bool>> ReadAsync(Device device, int channel, CancellationToken cancellationToken);

    Task<Result<bool>> SetAsync(Device device, int channel, bool on, CancellationToken cancellationToken);
}
