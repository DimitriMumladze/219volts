using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Events;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class DeviceStatuses(TimeProvider time, IEventQueue events, ILogger<DeviceStatuses> logger)
{
    private const int MissesBeforeOffline = 3;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, Entry> _entries = [];

    public DeviceStatus Get(int deviceId)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
            {
                return DeviceStatus.Unknown;
            }

            var isOnline = entry.State == State.Online;
            var channels = CopyOf(entry.Channels);
            return new DeviceStatus(isOnline, entry.LastSeen, channels);
        }
    }

    public OutletState StateOf(Outlet outlet, string deviceName)
    {
        var status = Get(outlet.DeviceId);
        var isOn = status.IsOn(outlet.Channel);
        return new OutletState(isOn, status.IsOnline, status.LastSeen, deviceName);
    }

    public void RecordOutcome(int deviceId, string? error)
    {
        lock (_lock)
        {
            var entry = GetOrCreateEntry(deviceId);
            if (error == DeviceError.NoAnswer)
            {
                RecordMiss(deviceId, entry);
            }
            else
            {
                RecordAnswer(deviceId, entry);
            }
        }
    }

    public void StoreChannels(int deviceId, IReadOnlyList<bool> states)
    {
        lock (_lock)
        {
            var entry = GetOrCreateEntry(deviceId);
            var before = entry.Channels;
            var known = states.Select(state => (bool?)state);
            entry.Channels = [.. known];
            for (var index = 0; index < entry.Channels.Length; index++)
            {
                var old = ValueAt(before, index);
                PublishIfChanged(deviceId, index + 1, old, entry.Channels[index]);
            }
        }
    }

    public void StoreChannel(int deviceId, int channelCount, int channel, bool on)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
            {
                return;
            }

            entry.Channels ??= new bool?[channelCount];
            var channels = entry.Channels;
            if (channel >= 1 && channel <= channels.Length)
            {
                var old = channels[channel - 1];
                channels[channel - 1] = on;
                PublishIfChanged(deviceId, channel, old, on);
            }
        }
    }

    public void ForgetChannel(int deviceId, int channel)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
            {
                return;
            }

            var channels = entry.Channels;
            if (channels is not null && channel >= 1 && channel <= channels.Length)
            {
                var old = channels[channel - 1];
                channels[channel - 1] = null;
                PublishIfChanged(deviceId, channel, old, null);
            }
        }
    }

    public void Remove(int deviceId)
    {
        lock (_lock)
        {
            _entries.Remove(deviceId);
        }
    }


    private void RecordAnswer(int deviceId, Entry entry)
    {
        if (entry.State == State.Offline)
        {
            logger.LogInformation("Device {DeviceId} is back online", deviceId);
        }

        var wasOnline = entry.State == State.Online;
        entry.State = State.Online;
        entry.Misses = 0;
        entry.LastSeen = time.GetUtcNow().UtcDateTime;
        if (!wasOnline)
        {
            events.Publish(new DeviceWentOnline(deviceId));
        }
    }

    private void RecordMiss(int deviceId, Entry entry)
    {
        entry.Misses++;
        if (entry.Misses >= MissesBeforeOffline && entry.State != State.Offline)
        {
            logger.LogWarning("Device {DeviceId} is offline after {Misses} missed checks", deviceId, entry.Misses);
            entry.State = State.Offline;
            entry.Channels = null;
            events.Publish(new DeviceWentOffline(deviceId));
        }
    }

    private void PublishIfChanged(int deviceId, int channel, bool? old, bool? current)
    {
        if (old != current)
        {
            var outlet = new Outlet(deviceId, channel);
            events.Publish(new OutletChanged(outlet, current));
        }
    }

    private static bool? ValueAt(bool?[]? channels, int index)
    {
        if (channels is null || index >= channels.Length)
        {
            return null;
        }

        return channels[index];
    }

    private static bool?[]? CopyOf(bool?[]? channels)
    {
        if (channels is null)
        {
            return null;
        }

        return [.. channels];
    }

    private Entry GetOrCreateEntry(int deviceId)
    {
        if (!_entries.TryGetValue(deviceId, out var entry))
        {
            entry = new Entry();
            _entries[deviceId] = entry;
        }

        return entry;
    }

    private enum State
    {
        Unknown,
        Online,
        Offline
    }

    private class Entry
    {
        public State State { get; set; } = State.Unknown;

        public int Misses { get; set; }

        public DateTime? LastSeen { get; set; }

        public bool?[]? Channels { get; set; }
    }
}
