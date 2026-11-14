using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

// Stands in for a real driver in service and endpoint tests. Keeps channel states per device
// and answers as the test says.
public class FakeRelayDriver(DeviceKind kind) : IRelayDriver
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, bool[]> _channels = [];

    public DeviceKind Kind { get; } = kind;

    // Same rules as the real driver of this kind, because services ask the driver for them.
    public int DefaultPort => Kind == DeviceKind.ShellyGen2 ? 80 : 502;

    public IReadOnlyList<int> AllowedChannelCounts => Kind == DeviceKind.ShellyGen2 ? [1, 2, 3, 4] : [8, 16, 30];

    // When set, every call fails with this error (DeviceError.NoAnswer or DeviceError.ErrorAnswer).
    public string? Error { get; set; }

    // When true, SetAsync answers OK but the relay does not change: the read-back then disagrees.
    public bool IgnoreSet { get; set; }

    // When set, ReadAllAsync waits for it, so a test can hold a check in the middle.
    public TaskCompletionSource? Gate { get; set; }

    public List<string> Calls { get; } = [];

    public void SetChannels(Device device, params bool[] states)
    {
        lock (_lock)
        {
            _channels[device.Id] = states;
        }
    }

    public async Task<Result<IReadOnlyList<bool>>> ReadAllAsync(Device device, CancellationToken cancellationToken)
    {
        Record($"read-all {device.Id}");
        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (Error is not null)
        {
            return Result<IReadOnlyList<bool>>.Fail(Error);
        }

        return Result<IReadOnlyList<bool>>.Ok([.. StatesOf(device)]);
    }

    public Task<Result<bool>> ReadAsync(Device device, int channel, CancellationToken cancellationToken)
    {
        Record($"read {device.Id}/{channel}");
        if (Error is not null)
        {
            return Task.FromResult(Result<bool>.Fail(Error));
        }

        return Task.FromResult(Result<bool>.Ok(StatesOf(device)[channel - 1]));
    }

    public Task<Result<bool>> SetAsync(Device device, int channel, bool on, CancellationToken cancellationToken)
    {
        Record($"set {device.Id}/{channel} {(on ? "on" : "off")}");
        if (Error is not null)
        {
            return Task.FromResult(Result<bool>.Fail(Error));
        }

        if (!IgnoreSet)
        {
            lock (_lock)
            {
                StatesOf(device)[channel - 1] = on;
            }
        }

        return Task.FromResult(Result<bool>.Ok(on));
    }

    private bool[] StatesOf(Device device)
    {
        lock (_lock)
        {
            if (!_channels.TryGetValue(device.Id, out var states))
            {
                states = new bool[device.ChannelCount];
                _channels[device.Id] = states;
            }

            return states;
        }
    }

    private void Record(string call)
    {
        lock (_lock)
        {
            Calls.Add(call);
        }
    }
}
