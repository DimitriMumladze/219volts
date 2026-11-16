using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class BoardGateTests
{
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TwoSeconds = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Check_answer_cannot_overwrite_a_later_switch()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        factory.Waveshare.Gate = new TaskCompletionSource();

        var check = CheckAsync(factory);
        var checkAsked = SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"read-all {panel.Id}"), FiveSeconds);
        var switching = SwitchAsync(factory, panel, 1, true);
        var switchFinishedEarly = SpinWait.SpinUntil(() => switching.IsCompleted, TimeSpan.FromMilliseconds(300));
        factory.Waveshare.Gate.SetResult();
        await check;
        var switched = await switching;

        var state = Statuses(factory).Get(panel.Id).IsOn(1);
        Assert.True(checkAsked);
        Assert.False(switchFinishedEarly);
        Assert.True(switched.Value);
        Assert.True(state);
    }

    [Fact]
    public async Task Waiting_more_than_2_s_at_the_door_is_no_answer_and_a_miss()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        await CheckAsync(factory);
        factory.Waveshare.Gate = new TaskCompletionSource();
        var check = CheckAsync(factory);
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Count(c => c == $"read-all {panel.Id}") == 2, FiveSeconds);

        var errors = new List<string?>();
        var finishedEarly = new List<bool>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var (early, result) = await SwitchGivingUpAsync(factory, panel);
            finishedEarly.Add(early);
            errors.Add(result.Error);
        }

        var whileBlocked = Statuses(factory).Get(panel.Id);
        factory.Waveshare.Gate.SetResult();
        await check;
        Assert.Equal([false, false, false], finishedEarly);
        Assert.Equal([DeviceError.NoAnswer, DeviceError.NoAnswer, DeviceError.NoAnswer], errors);
        Assert.False(whileBlocked.IsOnline);
        Assert.DoesNotContain($"set {panel.Id}/1 on", factory.Waveshare.Calls);
    }

    [Fact]
    public async Task Entering_a_second_gate_throws()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        var bar = await AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<BoardGate>();

        var nested = gate.RunAsync(panel, board => gate.RunAsync(bar, inner => inner.SwitchAsync(1, true)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => nested);
    }

    [Fact]
    public async Task Different_boards_do_not_wait_for_each_other()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        var bar = await AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        factory.Waveshare.Gate = new TaskCompletionSource();
        var check = CheckAsync(factory);
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"read-all {panel.Id}"), FiveSeconds);

        var switching = SwitchAsync(factory, bar, 1, true);
        var switched = await switching.WaitAsync(FiveSeconds);

        factory.Waveshare.Gate.SetResult();
        await check;
        Assert.True(switched.Value);
    }


    private static async Task<(bool FinishedEarly, Result<bool> Result)> SwitchGivingUpAsync(VenueAppFactory factory, Device device)
    {
        var oneTick = TimeSpan.FromTicks(1);
        var justUnderTwoSeconds = TwoSeconds - oneTick;
        var switching = SwitchAsync(factory, device, 1, true);

        factory.Time.Advance(justUnderTwoSeconds);
        var grace = Task.Delay(TimeSpan.FromMilliseconds(500));
        var first = await Task.WhenAny(switching, grace);
        var finishedEarly = first == switching;
        factory.Time.Advance(oneTick);
        var result = await switching.WaitAsync(FiveSeconds);

        return (finishedEarly, result);
    }

    private static async Task<Result<bool>> SwitchAsync(VenueAppFactory factory, Device device, int channel, bool on)
    {
        using var scope = factory.Services.CreateScope();
        var gate = scope.ServiceProvider.GetRequiredService<BoardGate>();
        return await gate.RunAsync(device, board => board.SwitchAsync(channel, on));
    }

    private static Task CheckAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<DeviceCheckService>(factory.Services).RunOnceAsync();

    private static DeviceStatuses Statuses(VenueAppFactory factory) =>
        factory.Services.GetRequiredService<DeviceStatuses>();

    private static async Task<Device> AddAsync(VenueAppFactory factory, string name, DeviceKind kind, string ip, int port, int channelCount)
    {
        var id = await TestDevices.AddAsync(factory, name, kind, ip, port, channelCount);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Devices.SingleAsync(d => d.Id == id);
    }
}
