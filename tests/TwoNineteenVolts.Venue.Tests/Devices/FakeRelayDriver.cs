using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class FakeRelayDriver(DeviceKind kind) : IRelayDriver
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, bool[]> _channels = [];

    public DeviceKind Kind { get; } = kind;

    public int DefaultPort => Kind == DeviceKind.ShellyGen2 ? 80 : 502;

    public IReadOnlyList<int> AllowedChannelCounts => Kind == DeviceKind.ShellyGen2 ? [1, 2, 3, 4] : [8, 16, 30];

    public string? Error { get; set; }

    public string? ReadError { get; set; }

    public bool IgnoreSet { get; set; }

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
        var states = StatesOf(device);
        bool[] answer;
        lock (_lock)
        {
            answer = [.. states];
        }

        if (Gate is not null)
        {
            await Gate.Task;
        }

        if (Error is not null)
        {
            return Result<IReadOnlyList<bool>>.Fail(Error);
        }

        return Result<IReadOnlyList<bool>>.Ok(answer);
    }

    public Task<Result<bool>> ReadAsync(Device device, int channel, CancellationToken cancellationToken)
    {
        Record($"read {device.Id}/{channel}");
        var error = Error ?? ReadError;
        if (error is not null)
        {
            var failure = Result<bool>.Fail(error);
            return Task.FromResult(failure);
        }

        var state = StatesOf(device)[channel - 1];
        var success = Result<bool>.Ok(state);
        return Task.FromResult(success);
    }

    public Task<Result<bool>> SetAsync(Device device, int channel, bool on, CancellationToken cancellationToken)
    {
        Record($"set {device.Id}/{channel} {(on ? "on" : "off")}");
        if (Error is not null)
        {
            var failure = Result<bool>.Fail(Error);
            return Task.FromResult(failure);
        }

        if (!IgnoreSet)
        {
            lock (_lock)
            {
                StatesOf(device)[channel - 1] = on;
            }
        }

        var success = Result<bool>.Ok(on);
        return Task.FromResult(success);
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
