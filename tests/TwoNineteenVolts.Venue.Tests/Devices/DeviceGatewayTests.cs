using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceGatewayTests
{
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Switch_turns_on_reads_back_and_remembers()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var outlet = new Outlet(panel, 3);

        var result = await UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));

        var state = await UseGatewayAsync(factory, devices => devices.StateOfAsync(outlet));
        Assert.False(result.IsFailure);
        Assert.True(result.Value);
        Assert.Contains($"set {panel}/3 on", factory.Waveshare.Calls);
        Assert.Contains($"read {panel}/3", factory.Waveshare.Calls);
        Assert.True(state.IsOn);
    }

    [Fact]
    public async Task Relay_that_does_not_switch_is_did_not_obey_and_its_state_is_remembered()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var outlet = new Outlet(panel, 3);
        factory.Waveshare.IgnoreSet = true;

        var result = await UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));

        var state = await UseGatewayAsync(factory, devices => devices.StateOfAsync(outlet));
        Assert.Equal(DeviceError.DidNotObey, result.Error);
        Assert.False(state.IsOn);
    }

    [Theory]
    [InlineData(DeviceError.NoAnswer)]
    [InlineData(DeviceError.ErrorAnswer)]
    public async Task No_answer_and_error_answer_pass_through(string error)
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        factory.Waveshare.Error = error;

        var result = await UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(panel, 3), true));

        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task State_of_an_outlet_has_board_name_online_and_last_seen()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var outlet = new Outlet(panel, 3);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        await UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));

        var state = await UseGatewayAsync(factory, devices => devices.StateOfAsync(outlet));

        Assert.Equal(new OutletState(true, true, now, "Panel 1"), state);
    }

    [Fact]
    public async Task States_of_many_outlets_come_in_one_call()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var bar = await TestDevices.AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        var first = new Outlet(panel, 1);
        var second = new Outlet(panel, 2);
        var barOutlet = new Outlet(bar, 1);
        var unknown = new Outlet(99, 1);
        List<Outlet> outlets = [first, second, barOutlet, unknown];

        var states = await UseGatewayAsync(factory, devices => devices.StatesOfAsync(outlets));

        var keys = states.Keys.ToHashSet();
        Assert.Equal([first, second, barOutlet], keys);
        Assert.Equal("Panel 1", states[second].DeviceName);
        Assert.Equal("Bar", states[barOutlet].DeviceName);
        Assert.Null(states[first].IsOn);
    }

    [Fact]
    public async Task Check_outlet_reports_unknown_board_and_channel_out_of_range()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);

        var fine = await UseGatewayAsync(factory, devices => devices.CheckOutletAsync(new Outlet(panel, 16)));
        var zero = await UseGatewayAsync(factory, devices => devices.CheckOutletAsync(new Outlet(panel, 0)));
        var tooHigh = await UseGatewayAsync(factory, devices => devices.CheckOutletAsync(new Outlet(panel, 17)));
        var unknown = await UseGatewayAsync(factory, devices => devices.CheckOutletAsync(new Outlet(99, 1)));

        Assert.Equal(OutletCheck.Fine, fine);
        Assert.Equal(OutletCheck.ChannelOutOfRange, zero);
        Assert.Equal(OutletCheck.ChannelOutOfRange, tooHigh);
        Assert.Equal(OutletCheck.UnknownDevice, unknown);
    }

    [Fact]
    public async Task Forget_state_makes_the_outlet_unknown()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var outlet = new Outlet(panel, 3);
        await UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));
        bool? stateWhileHolding = null;

        await UseGatewayAsync(factory, devices => devices.WithOutletAsync(outlet, control =>
        {
            stateWhileHolding = control.State.IsOn;
            control.ForgetState();
            var done = Result<bool>.Ok(true);
            return Task.FromResult(done);
        }));

        var state = await UseGatewayAsync(factory, devices => devices.StateOfAsync(outlet));
        Assert.True(stateWhileHolding);
        Assert.Null(state.IsOn);
    }

    [Fact]
    public async Task Holding_an_outlet_twice_in_one_flow_throws()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var bar = await TestDevices.AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        using var scope = factory.Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceGateway>();

        var nested = devices.WithOutletAsync(new Outlet(panel, 1), _ => devices.SwitchAsync(new Outlet(bar, 1), true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => nested);
    }

    [Fact]
    public async Task Check_answer_cannot_overwrite_a_later_switch()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var outlet = new Outlet(panel, 1);
        factory.Waveshare.Gate = new TaskCompletionSource();

        var check = CheckAsync(factory);
        var checkAsked = SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"read-all {panel}"), FiveSeconds);
        var switching = UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));
        var switchFinishedEarly = SpinWait.SpinUntil(() => switching.IsCompleted, TimeSpan.FromMilliseconds(300));
        factory.Waveshare.Gate.SetResult();
        await check;
        var switched = await switching;

        var state = await UseGatewayAsync(factory, devices => devices.StateOfAsync(outlet));
        Assert.True(checkAsked);
        Assert.False(switchFinishedEarly);
        Assert.True(switched.Value);
        Assert.True(state.IsOn);
    }

    [Fact]
    public async Task Waiting_more_than_2_s_for_a_board_is_no_answer_and_a_miss()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await CheckAsync(factory);
        factory.Waveshare.Gate = new TaskCompletionSource();
        var check = CheckAsync(factory);
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Count(c => c == $"read-all {panel}") == 2, FiveSeconds);

        var errors = new List<string?>();
        var finishedEarly = new List<bool>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var (early, result) = await SwitchGivingUpAsync(factory, new Outlet(panel, 1));
            finishedEarly.Add(early);
            errors.Add(result.Error);
        }

        var whileBlocked = await UseGatewayAsync(factory, devices => devices.StateOfAsync(new Outlet(panel, 1)));
        factory.Waveshare.Gate.SetResult();
        await check;
        Assert.Equal([false, false, false], finishedEarly);
        Assert.Equal([DeviceError.NoAnswer, DeviceError.NoAnswer, DeviceError.NoAnswer], errors);
        Assert.False(whileBlocked.DeviceOnline);
        Assert.DoesNotContain($"set {panel}/1 on", factory.Waveshare.Calls);
    }

    [Fact]
    public async Task Holding_an_outlet_keeps_other_calls_on_that_board_waiting()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var release = new TaskCompletionSource();
        var held = new TaskCompletionSource();

        var holding = UseGatewayAsync(factory, devices => devices.WithOutletAsync(new Outlet(panel, 1), async _ =>
        {
            held.SetResult();
            await release.Task;
            return Result<bool>.Ok(true);
        }));
        await held.Task.WaitAsync(FiveSeconds);
        var switching = UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(panel, 2), true));
        var switchFinishedEarly = SpinWait.SpinUntil(() => switching.IsCompleted, TimeSpan.FromMilliseconds(300));
        release.SetResult();
        await holding;
        var switched = await switching.WaitAsync(FiveSeconds);

        Assert.False(switchFinishedEarly);
        Assert.True(switched.Value);
    }

    [Fact]
    public async Task Different_boards_do_not_wait_for_each_other()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var bar = await TestDevices.AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        factory.Waveshare.Gate = new TaskCompletionSource();
        var check = CheckAsync(factory);
        SpinWait.SpinUntil(() => factory.Waveshare.Calls.Contains($"read-all {panel}"), FiveSeconds);

        var switching = UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(bar, 1), true));
        var switched = await switching.WaitAsync(FiveSeconds);

        factory.Waveshare.Gate.SetResult();
        await check;
        Assert.True(switched.Value);
    }

    [Fact]
    public async Task Gateway_never_opens_two_connections_to_one_board()
    {
        using var factory = new VenueAppFactory();
        factory.ExtraServices = services =>
        {
            services.RemoveAll<IRelayDriver>();
            services.AddSingleton<IRelayDriver>(factory.Shelly);
            services.AddSingleton<IRelayDriver, ModbusRelayDriver>();
        };
        var silentBoard = new TcpListener(IPAddress.Loopback, 0);
        silentBoard.Start();
        var endpoint = (IPEndPoint)silentBoard.LocalEndpoint;
        var connections = new List<TcpClient>();
        var accepting = AcceptAllAsync(silentBoard, connections);
        var board = await TestDevices.AddAsync(factory, "Silent", DeviceKind.WaveshareModbusTcp, "127.0.0.1", endpoint.Port, 16);

        try
        {
            var first = UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(board, 1), true));
            var firstConnected = SpinWait.SpinUntil(() => CountOf(connections) == 1, FiveSeconds);
            var second = UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(board, 2), true));
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            var connectionsWhileFirstWaits = CountOf(connections);
            var firstStillWaiting = !first.IsCompleted;
            await Task.WhenAll(first, second);
            var connectionsAtTheEnd = CountOf(connections);

            Assert.True(firstConnected);
            Assert.True(firstStillWaiting);
            Assert.Equal(1, connectionsWhileFirstWaits);
            Assert.Equal(2, connectionsAtTheEnd);
        }
        finally
        {
            silentBoard.Stop();
            await accepting;
            lock (connections)
            {
                connections.ForEach(c => c.Dispose());
            }
        }
    }

    [Fact]
    public async Task Holding_an_unknown_device_inside_a_hold_still_throws()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        using var scope = factory.Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceGateway>();

        var nested = devices.WithOutletAsync(new Outlet(panel, 1), _ => devices.SwitchAsync(new Outlet(99, 1), true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => nested);
    }

    [Fact]
    public async Task Unknown_board_is_device_not_found()
    {
        using var factory = new VenueAppFactory();

        var result = await UseGatewayAsync(factory, devices => devices.SwitchAsync(new Outlet(99, 1), true));

        Assert.Equal(DeviceError.NotFound, result.Error);
    }


    private static async Task<(bool FinishedEarly, Result<bool> Result)> SwitchGivingUpAsync(VenueAppFactory factory, Outlet outlet)
    {
        var oneTick = TimeSpan.FromTicks(1);
        var justUnderTwoSeconds = TimeSpan.FromSeconds(2) - oneTick;
        var switching = UseGatewayAsync(factory, devices => devices.SwitchAsync(outlet, true));
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        factory.Time.Advance(justUnderTwoSeconds);
        var grace = Task.Delay(TimeSpan.FromMilliseconds(500));
        var first = await Task.WhenAny(switching, grace);
        var finishedEarly = first == switching;
        factory.Time.Advance(oneTick);
        var result = await switching.WaitAsync(FiveSeconds);

        return (finishedEarly, result);
    }

    private static Task CheckAsync(VenueAppFactory factory) =>
        ActivatorUtilities.CreateInstance<DeviceCheckService>(factory.Services).RunOnceAsync();

    private static async Task AcceptAllAsync(TcpListener listener, List<TcpClient> connections)
    {
        try
        {
            while (true)
            {
                var connection = await listener.AcceptTcpClientAsync();
                lock (connections)
                {
                    connections.Add(connection);
                }
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static int CountOf(List<TcpClient> connections)
    {
        lock (connections)
        {
            return connections.Count;
        }
    }

    private static Task<int> AddPanelAsync(VenueAppFactory factory)
    {
        return TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
    }

    private static async Task<T> UseGatewayAsync<T>(VenueAppFactory factory, Func<IDeviceGateway, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceGateway>();
        return await action(devices);
    }
}
