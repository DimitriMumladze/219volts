using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentModbus;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class ModbusRelayDriverTests
{
    private const byte UnitId = 1;

    private static readonly DateTime CreatedAt = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Kind_default_port_and_channel_counts_are_waveshare_502_and_8_16_30()
    {
        var driver = new ModbusRelayDriver();

        Assert.Equal(DeviceKind.WaveshareModbusTcp, driver.Kind);
        Assert.Equal(502, driver.DefaultPort);
        Assert.Equal([8, 16, 30], driver.AllowedChannelCounts);
    }

    [Fact]
    public async Task Set_writes_the_coil_of_channel_minus_one()
    {
        using var server = StartServer(out var port);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 16);

        var on = await driver.SetAsync(device, 3, true, CancellationToken.None);
        var coilTwo = CoilOf(server, 2);
        var off = await driver.SetAsync(device, 3, false, CancellationToken.None);
        var coilTwoAfterOff = CoilOf(server, 2);

        Assert.True(on.Value);
        Assert.False(off.Value);
        Assert.True(coilTwo);
        Assert.False(coilTwoAfterOff);
    }

    [Fact]
    public async Task Read_returns_the_coil_state()
    {
        using var server = StartServer(out var port);
        SetCoils(server, 4);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 16);

        var channelFive = await driver.ReadAsync(device, 5, CancellationToken.None);
        var channelSix = await driver.ReadAsync(device, 6, CancellationToken.None);

        Assert.True(channelFive.Value);
        Assert.False(channelSix.Value);
    }

    [Fact]
    public async Task ReadAll_returns_channel_count_states_in_order()
    {
        using var server = StartServer(out var port);
        SetCoils(server, 0, 2, 8, 15);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 16);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        bool[] expected =
        [
            true, false, true, false, false, false, false, false,
            true, false, false, false, false, false, false, true
        ];
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task ReadAll_of_30_channels_reads_all_four_bytes()
    {
        using var server = StartServer(out var port);
        SetCoils(server, 0, 9, 17, 24, 29);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 30);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        var onChannels = result.Value!
            .Select((on, index) => (on, channel: index + 1))
            .Where(c => c.on)
            .Select(c => c.channel);
        Assert.Equal(30, result.Value!.Count);
        Assert.Equal([1, 10, 18, 25, 30], onChannels);
    }

    [Fact]
    public async Task Modbus_exception_reply_is_an_error_answer()
    {
        using var server = StartServer(out var port);
        server.RequestValidator = (_, _, _, _) => ModbusExceptionCode.IllegalDataAddress;
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 16);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        Assert.Equal(DeviceError.ErrorAnswer, result.Error);
    }

    [Fact]
    public async Task Connection_refused_is_no_answer()
    {
        var port = FreePort();
        var driver = new ModbusRelayDriver();
        var device = BoardOn(port, channelCount: 16);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        Assert.Equal(DeviceError.NoAnswer, result.Error);
    }

    [Fact]
    public async Task Timeout_is_no_answer()
    {
        var silentBoard = new TcpListener(IPAddress.Loopback, 0);
        silentBoard.Start();
        var endpoint = (IPEndPoint)silentBoard.LocalEndpoint;
        var driver = new ModbusRelayDriver();
        var device = BoardOn(endpoint.Port, channelCount: 16);

        try
        {
            var result = await driver.ReadAllAsync(device, CancellationToken.None);

            Assert.Equal(DeviceError.NoAnswer, result.Error);
        }
        finally
        {
            silentBoard.Stop();
        }
    }

    [Fact]
    public async Task Calls_to_one_board_never_overlap()
    {
        var silentBoard = new TcpListener(IPAddress.Loopback, 0);
        silentBoard.Start();
        var endpoint = (IPEndPoint)silentBoard.LocalEndpoint;
        var connections = new List<TcpClient>();
        var accepting = AcceptAllAsync(silentBoard, connections);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(endpoint.Port, channelCount: 16);
        var fiveSeconds = TimeSpan.FromSeconds(5);
        var halfSecond = TimeSpan.FromMilliseconds(500);

        try
        {
            var first = driver.ReadAllAsync(device, CancellationToken.None);
            var firstConnected = SpinWait.SpinUntil(() => CountOf(connections) == 1, fiveSeconds);
            var second = driver.ReadAllAsync(device, CancellationToken.None);
            await Task.Delay(halfSecond);
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
    public async Task Unreachable_boards_do_not_wait_for_each_other()
    {
        var firstPort = FreePort();
        var secondPort = FreePort();
        var driver = new ModbusRelayDriver();
        var first = BoardOn(firstPort, channelCount: 16);
        var second = BoardOn(secondPort, channelCount: 16);
        var stopwatch = Stopwatch.StartNew();

        var firstCall = driver.ReadAllAsync(first, CancellationToken.None);
        var secondCall = driver.ReadAllAsync(second, CancellationToken.None);
        await Task.WhenAll(firstCall, secondCall);

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
        Assert.True(elapsedSeconds < 3.5, $"took {elapsedSeconds:0.0} s");
    }

    [Fact]
    public async Task Board_that_drops_the_connection_is_no_answer()
    {
        var rudeBoard = new TcpListener(IPAddress.Loopback, 0);
        rudeBoard.Start();
        var endpoint = (IPEndPoint)rudeBoard.LocalEndpoint;
        var closing = CloseEachConnectionAsync(rudeBoard);
        var driver = new ModbusRelayDriver();
        var device = BoardOn(endpoint.Port, channelCount: 16);

        try
        {
            var result = await driver.ReadAllAsync(device, CancellationToken.None);

            Assert.Equal(DeviceError.NoAnswer, result.Error);
        }
        finally
        {
            rudeBoard.Stop();
            await closing;
        }
    }


    private static async Task CloseEachConnectionAsync(TcpListener listener)
    {
        try
        {
            while (true)
            {
                using var connection = await listener.AcceptTcpClientAsync();
                var stream = connection.GetStream();
                var request = new byte[64];
                var requestLength = await stream.ReadAsync(request);
                connection.Client.Shutdown(SocketShutdown.Send);
                var remainingLength = await stream.ReadAsync(request);
            }
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static ModbusTcpServer StartServer(out int port)
    {
        port = FreePort();
        var server = new ModbusTcpServer();
        server.AddUnit(UnitId);
        var endpoint = new IPEndPoint(IPAddress.Loopback, port);
        server.Start(endpoint);
        return server;
    }

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
        catch (InvalidOperationException)
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

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        listener.Stop();
        return endpoint.Port;
    }

    private static Device BoardOn(int port, int channelCount)
    {
        return new Device("Panel 1", DeviceKind.WaveshareModbusTcp, "127.0.0.1", port, channelCount, CreatedAt);
    }

    private static void SetCoils(ModbusTcpServer server, params int[] addresses)
    {
        lock (server.Lock)
        {
            var coils = server.GetCoils(UnitId);
            foreach (var address in addresses)
            {
                coils.Set(address, true);
            }
        }
    }

    private static bool CoilOf(ModbusTcpServer server, int address)
    {
        lock (server.Lock)
        {
            var coils = server.GetCoils(UnitId);
            return coils.Get(address);
        }
    }
}
