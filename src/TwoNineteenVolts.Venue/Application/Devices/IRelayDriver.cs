using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices;

public interface IRelayDriver
{
    DeviceKind Kind { get; }

    int DefaultPort { get; }

    IReadOnlyList<int> AllowedChannelCounts { get; }

    Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken);

    Task<Result<bool>> ReadAsync(Device device, int channel, CancellationToken cancellationToken);

    Task<Result<bool>> SetAsync(Device device, int channel, bool on, CancellationToken cancellationToken);
}
