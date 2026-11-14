using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using TwoNineteenVolts.Venue.Application.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceStatusesTests
{
    private const int DeviceId = 3;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<DeviceStatuses> _logger = new();

    [Fact]
    public void Unknown_device_is_offline_with_unknown_states()
    {
        var statuses = NewStatuses();

        var status = statuses.Get(DeviceId);

        Assert.False(status.IsOnline);
        Assert.Null(status.LastSeen);
        Assert.Null(status.Channels);
        Assert.Null(status.IsOn(1));
    }

    [Fact]
    public void Answer_makes_device_online_and_sets_last_seen()
    {
        var statuses = NewStatuses();

        statuses.RecordOutcome(DeviceId, error: null);

        var status = statuses.Get(DeviceId);
        Assert.True(status.IsOnline);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, status.LastSeen);
    }

    [Fact]
    public void Error_answer_counts_as_reachable()
    {
        var statuses = NewStatuses();

        statuses.RecordOutcome(DeviceId, DeviceError.ErrorAnswer);

        var status = statuses.Get(DeviceId);
        Assert.True(status.IsOnline);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, status.LastSeen);
    }

    [Fact]
    public void Device_goes_offline_only_after_three_misses_in_a_row()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);
        var seen = _time.GetUtcNow().UtcDateTime;
        _time.Advance(TimeSpan.FromSeconds(10));

        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        Assert.True(statuses.Get(DeviceId).IsOnline);

        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        var status = statuses.Get(DeviceId);
        Assert.False(status.IsOnline);
        Assert.Equal(seen, status.LastSeen);
    }

    [Fact]
    public void An_answer_resets_the_miss_count()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);
        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);

        statuses.RecordOutcome(DeviceId, error: null);
        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);

        Assert.True(statuses.Get(DeviceId).IsOnline);
    }

    [Fact]
    public void Offline_device_reports_unknown_channel_states()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);
        statuses.StoreChannels(DeviceId, [true, false]);

        GoOffline(statuses);

        Assert.Null(statuses.Get(DeviceId).IsOn(1));
    }

    [Fact]
    public void Stored_channel_state_is_returned_while_online()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);

        statuses.StoreChannels(DeviceId, [true, false, false]);
        statuses.StoreChannel(DeviceId, channel: 3, on: true);

        var status = statuses.Get(DeviceId);
        Assert.True(status.IsOn(1));
        Assert.False(status.IsOn(2));
        Assert.True(status.IsOn(3));
    }

    [Fact]
    public void Reset_starts_the_status_over()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);
        statuses.StoreChannels(DeviceId, [true]);

        statuses.Reset(DeviceId);

        var status = statuses.Get(DeviceId);
        Assert.False(status.IsOnline);
        Assert.Null(status.LastSeen);
        Assert.Null(status.Channels);
    }

    [Fact]
    public void Remove_forgets_the_device()
    {
        var statuses = NewStatuses();
        statuses.RecordOutcome(DeviceId, error: null);
        statuses.StoreChannels(DeviceId, [true]);

        statuses.Remove(DeviceId);

        var status = statuses.Get(DeviceId);
        Assert.False(status.IsOnline);
        Assert.Null(status.LastSeen);
        Assert.Null(status.Channels);
    }

    [Fact]
    public void Logs_once_when_going_offline_and_once_when_back()
    {
        var statuses = NewStatuses();

        // The first answer after a start is not "back online" (Q127).
        statuses.RecordOutcome(DeviceId, error: null);
        Assert.Empty(_logger.Collector.GetSnapshot());

        for (var miss = 1; miss <= 5; miss++)
        {
            statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        }

        statuses.RecordOutcome(DeviceId, error: null);
        statuses.RecordOutcome(DeviceId, error: null);

        Assert.Equal([LogLevel.Warning, LogLevel.Information], _logger.Collector.GetSnapshot().Select(r => r.Level));
    }

    [Fact]
    public void Device_never_seen_since_start_logs_offline_once()
    {
        var statuses = NewStatuses();

        for (var miss = 1; miss <= 5; miss++)
        {
            statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        }

        Assert.Equal([LogLevel.Warning], _logger.Collector.GetSnapshot().Select(r => r.Level));
    }

    private DeviceStatuses NewStatuses() => new(_time, _logger);

    private static void GoOffline(DeviceStatuses statuses)
    {
        for (var miss = 1; miss <= 3; miss++)
        {
            statuses.RecordOutcome(DeviceId, DeviceError.NoAnswer);
        }
    }
}
