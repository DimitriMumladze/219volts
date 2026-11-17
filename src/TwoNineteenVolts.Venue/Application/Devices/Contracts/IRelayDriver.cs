using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Application.Devices.Contracts;

public interface IRelayDriver
{
    DeviceKind Kind { get; }

    int DefaultPort { get; }

    IReadOnlyList<int> AllowedChannelCounts { get; }

    Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken);

    Task<Result<bool>> ReadIsOnAsync(Device device, int channel, CancellationToken cancellationToken);

    Task<Result> SetAsync(Device device, int channel, bool shouldBeOn, CancellationToken cancellationToken);
}
