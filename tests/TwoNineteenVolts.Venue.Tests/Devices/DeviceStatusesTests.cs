using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Devices.Events;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Tests.Events;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceStatusesTests
{
    private const int DeviceId = 3;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeLogger<DeviceStatuses> _logger = new();
    private readonly RecordingEventQueue _events = new();

    [Fact]
    public void First_answer_publishes_went_online()
    {
        var statuses = NewStatuses();

        statuses.RecordAnswer(DeviceId);
        statuses.RecordAnswer(DeviceId);

        Assert.Equal([new DeviceWentOnline(DeviceId)], _events.Events);
    }

    [Fact]
    public void Third_miss_publishes_went_offline_once()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        _events.Clear();

        GoOffline(statuses);
        statuses.RecordMiss(DeviceId);

        Assert.Equal([new DeviceWentOffline(DeviceId)], _events.Events);
    }

    [Fact]
    public void Same_channels_again_publish_nothing()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        statuses.StoreChannels(DeviceId, [true, false]);
        _events.Clear();

        statuses.RecordAnswer(DeviceId);
        statuses.StoreChannels(DeviceId, [true, false]);

        Assert.Empty(_events.Events);
    }

    [Fact]
    public void Changed_channel_publishes_channel_state_changed()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        _events.Clear();

        statuses.StoreChannels(DeviceId, [true, false]);
        statuses.StoreChannels(DeviceId, [true, true]);
        statuses.StoreChannel(DeviceId, 2, 1, false);
        statuses.ForgetChannel(DeviceId, 2);

        List<IVenueEvent> expected =
        [
            new OutletChanged(new Outlet(DeviceId, 1), true),
            new OutletChanged(new Outlet(DeviceId, 2), false),
            new OutletChanged(new Outlet(DeviceId, 2), true),
            new OutletChanged(new Outlet(DeviceId, 1), false),
            new OutletChanged(new Outlet(DeviceId, 2), null)
        ];
        Assert.Equal(expected, _events.Events);
    }

    [Fact]
    public void Unknown_device_is_offline_with_unknown_states()
    {
        var statuses = NewStatuses();

        var status = statuses.Get(DeviceId);

        Assert.False(status.IsOnline);
        Assert.Null(status.LastSeen);
        var channelOne = status.IsOn(1);
        Assert.Null(status.Channels);
        Assert.Null(channelOne);
    }

    [Fact]
    public void Answer_makes_device_online_and_sets_last_seen()
    {
        var statuses = NewStatuses();

        statuses.RecordAnswer(DeviceId);

        var status = statuses.Get(DeviceId);
        var now = _time.GetUtcNow().UtcDateTime;
        Assert.True(status.IsOnline);
        Assert.Equal(now, status.LastSeen);
    }

    [Fact]
    public void Error_answer_counts_as_reachable()
    {
        var statuses = NewStatuses();

        statuses.RecordAnswer(DeviceId);

        var status = statuses.Get(DeviceId);
        var now = _time.GetUtcNow().UtcDateTime;
        Assert.True(status.IsOnline);
        Assert.Equal(now, status.LastSeen);
    }

    [Fact]
    public void Device_goes_offline_only_after_three_misses_in_a_row()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        var seen = _time.GetUtcNow().UtcDateTime;
        var tenSeconds = TimeSpan.FromSeconds(10);
        _time.Advance(tenSeconds);

        statuses.RecordMiss(DeviceId);
        statuses.RecordMiss(DeviceId);
        var afterTwoMisses = statuses.Get(DeviceId);
        Assert.True(afterTwoMisses.IsOnline);

        statuses.RecordMiss(DeviceId);
        var status = statuses.Get(DeviceId);
        Assert.False(status.IsOnline);
        Assert.Equal(seen, status.LastSeen);
    }

    [Fact]
    public void An_answer_resets_the_miss_count()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        statuses.RecordMiss(DeviceId);
        statuses.RecordMiss(DeviceId);

        statuses.RecordAnswer(DeviceId);
        statuses.RecordMiss(DeviceId);
        statuses.RecordMiss(DeviceId);

        var status = statuses.Get(DeviceId);
        Assert.True(status.IsOnline);
    }

    [Fact]
    public void Offline_device_reports_unknown_channel_states()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        statuses.StoreChannels(DeviceId, [true, false]);

        GoOffline(statuses);

        var status = statuses.Get(DeviceId);
        var channelOne = status.IsOn(1);
        Assert.Null(channelOne);
    }

    [Fact]
    public void Single_channel_is_known_before_the_first_full_read()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);

        statuses.StoreChannel(DeviceId, channelCount: 4, channel: 2, isOn: true);

        var status = statuses.Get(DeviceId);
        var channelOne = status.IsOn(1);
        var channelTwo = status.IsOn(2);
        Assert.Null(channelOne);
        Assert.True(channelTwo);
        Assert.Equal(4, status.Channels!.Count);
    }

    [Fact]
    public void Going_offline_forgets_channel_states()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        statuses.StoreChannels(DeviceId, [true, false]);

        GoOffline(statuses);
        statuses.RecordAnswer(DeviceId);

        var status = statuses.Get(DeviceId);
        var channelOne = status.IsOn(1);
        Assert.True(status.IsOnline);
        Assert.Null(channelOne);
    }

    [Fact]
    public void Forgotten_channel_is_unknown_and_others_stay_known()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
        statuses.StoreChannels(DeviceId, [true, true, false]);

        statuses.ForgetChannel(DeviceId, 2);

        var status = statuses.Get(DeviceId);
        var channelOne = status.IsOn(1);
        var channelTwo = status.IsOn(2);
        var channelThree = status.IsOn(3);
        Assert.True(channelOne);
        Assert.Null(channelTwo);
        Assert.False(channelThree);
    }

    [Fact]
    public void Stored_channel_state_is_returned_while_online()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);

        statuses.StoreChannels(DeviceId, [true, false, false]);
        statuses.StoreChannel(DeviceId, channelCount: 3, channel: 3, isOn: true);

        var status = statuses.Get(DeviceId);
        var channelOne = status.IsOn(1);
        var channelTwo = status.IsOn(2);
        var channelThree = status.IsOn(3);
        Assert.True(channelOne);
        Assert.False(channelTwo);
        Assert.True(channelThree);
    }

    [Fact]
    public void Remove_forgets_the_device()
    {
        var statuses = NewStatuses();
        statuses.RecordAnswer(DeviceId);
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

        statuses.RecordAnswer(DeviceId);
        var afterFirstAnswer = _logger.Collector.GetSnapshot();
        Assert.Empty(afterFirstAnswer);

        for (var miss = 1; miss <= 5; miss++)
        {
            statuses.RecordMiss(DeviceId);
        }

        statuses.RecordAnswer(DeviceId);
        statuses.RecordAnswer(DeviceId);

        var levels = _logger.Collector.GetSnapshot().Select(r => r.Level);
        Assert.Equal([LogLevel.Warning, LogLevel.Information], levels);
    }

    [Fact]
    public void Device_never_seen_since_start_logs_offline_once()
    {
        var statuses = NewStatuses();

        for (var miss = 1; miss <= 5; miss++)
        {
            statuses.RecordMiss(DeviceId);
        }

        var levels = _logger.Collector.GetSnapshot().Select(r => r.Level);
        Assert.Equal([LogLevel.Warning], levels);
    }


    private DeviceStatuses NewStatuses() => new(_time, _events, _logger);

    private static void GoOffline(DeviceStatuses statuses)
    {
        for (var miss = 1; miss <= 3; miss++)
        {
            statuses.RecordMiss(DeviceId);
        }
    }
}
