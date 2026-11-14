using Microsoft.Extensions.Logging;

namespace TwoNineteenVolts.Venue.Application.Devices;

// The live status of every device, in memory only: a restart starts everything over.
// The background check and staff switches update it at the same time, so every access takes the lock.
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

            return new DeviceStatus(entry.State == State.Online, entry.LastSeen, entry.Channels);
        }
    }

    // error: null or DeviceError.ErrorAnswer = the device answered; DeviceError.NoAnswer = a miss.
    public void RecordOutcome(int deviceId, string? error)
    {
        lock (_lock)
        {
            var entry = EntryFor(deviceId);
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
            EntryFor(deviceId).Channels = [.. states];
        }
    }

    // Ignored until the device's channels have been read once: one known channel alone
    // would make the others look known too.
    public void StoreChannel(int deviceId, int channel, bool on)
    {
        lock (_lock)
        {
            var channels = EntryFor(deviceId).Channels;
            if (channels is not null && channel >= 1 && channel <= channels.Length)
            {
                channels[channel - 1] = on;
            }
        }
    }

    // After an IP or port change: the old answers say nothing about the new address.
    public void Reset(int deviceId)
    {
        lock (_lock)
        {
            _entries.Remove(deviceId);
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
        // A device that answers for the first time after a start was never "offline" (Q127).
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
            // Also for a device never seen since the start, so a dead device is not silent (Q128).
            logger.LogWarning("Device {DeviceId} is offline after {Misses} missed checks", deviceId, entry.Misses);
            entry.State = State.Offline;
        }
    }

    private Entry EntryFor(int deviceId)
    {
        if (!_entries.TryGetValue(deviceId, out var entry))
        {
            entry = new Entry();
            _entries[deviceId] = entry;
        }

        return entry;
    }

    // Unknown: not answered yet since the start. Offline: missed 3 checks in a row.
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

        public bool[]? Channels { get; set; }
    }
}
