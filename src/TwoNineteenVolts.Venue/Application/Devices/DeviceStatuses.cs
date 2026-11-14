using Microsoft.Extensions.Logging;

namespace TwoNineteenVolts.Venue.Application.Devices;

public class DeviceStatuses(TimeProvider time, ILogger<DeviceStatuses> logger)
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
            var known = states.Select(state => (bool?)state);
            entry.Channels = [.. known];
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
                channels[channel - 1] = on;
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
                channels[channel - 1] = null;
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

        entry.State = State.Online;
        entry.Misses = 0;
        entry.LastSeen = time.GetUtcNow().UtcDateTime;
    }

    private void RecordMiss(int deviceId, Entry entry)
    {
        entry.Misses++;
        if (entry.Misses >= MissesBeforeOffline && entry.State != State.Offline)
        {
            logger.LogWarning("Device {DeviceId} is offline after {Misses} missed checks", deviceId, entry.Misses);
            entry.State = State.Offline;
            entry.Channels = null;
        }
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
