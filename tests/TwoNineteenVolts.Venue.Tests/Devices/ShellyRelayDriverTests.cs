using System.Net;
using System.Net.Sockets;
using System.Text;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class ShellyRelayDriverTests
{
    private static readonly DateTime CreatedAt = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Kind_default_port_and_channel_counts_are_shelly_80_and_1_to_4()
    {
        var handler = Answering("{}");
        var driver = DriverFor(handler);

        Assert.Equal(DeviceKind.ShellyGen2, driver.Kind);
        Assert.Equal(80, driver.DefaultPort);
        Assert.Equal([1, 2, 3, 4], driver.AllowedChannelCounts);
    }

    [Fact]
    public async Task Set_calls_switch_set_with_zero_based_id_on_the_devices_port()
    {
        var handler = Answering("""{"was_on":false}""");
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 4);

        var switchOn = await driver.SetAsync(device, 2, true, CancellationToken.None);
        var switchOff = await driver.SetAsync(device, 4, false, CancellationToken.None);

        var urls = handler.Requests.Select(u => u.ToString());
        Assert.False(switchOn.IsFailure);
        Assert.False(switchOff.IsFailure);
        Assert.Equal(
            [
                "http://192.168.10.30:8080/rpc/Switch.Set?id=1&on=true",
                "http://192.168.10.30:8080/rpc/Switch.Set?id=3&on=false"
            ],
            urls);
    }

    [Fact]
    public async Task Read_calls_switch_get_status_and_returns_output()
    {
        var handler = Answering("""{"id":0,"source":"init","output":true,"apower":0.0}""");
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var result = await driver.ReadIsOnAsync(device, 1, CancellationToken.None);

        var url = handler.Requests.Single().ToString();
        Assert.True(result.Value);
        Assert.Equal("http://192.168.10.30:8080/rpc/Switch.GetStatus?id=0", url);
    }

    [Fact]
    public async Task ReadAll_reads_every_channel_from_one_shelly_get_status_call()
    {
        var handler = Answering("""
            {
              "sys": {"mac": "A0B1C2D3E4F5"},
              "switch:0": {"id": 0, "output": true},
              "switch:1": {"id": 1, "output": false},
              "switch:2": {"id": 2, "output": false},
              "switch:3": {"id": 3, "output": true}
            }
            """);
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 4);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        var url = handler.Requests.Single().ToString();
        Assert.Equal([true, false, false, true], result.Value);
        Assert.Equal("http://192.168.10.30:8080/rpc/Shelly.GetStatus", url);
    }

    [Fact]
    public async Task ReadAll_with_a_missing_channel_is_an_error_answer()
    {
        var handler = Answering("""{"switch:0": {"id": 0, "output": true}}""");
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 4);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        Assert.Equal(DeviceError.ErrorAnswer, result.Error);
    }

    [Fact]
    public async Task Non_success_status_is_an_error_answer()
    {
        var handler = new FakeShellyHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return Task.FromResult(response);
        });
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var result = await driver.SetAsync(device, 1, true, CancellationToken.None);

        Assert.Equal(DeviceError.ErrorAnswer, result.Error);
    }

    [Fact]
    public async Task Unreadable_reply_is_an_error_answer()
    {
        var handler = Answering("this is not json");
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var result = await driver.ReadIsOnAsync(device, 1, CancellationToken.None);

        Assert.Equal(DeviceError.ErrorAnswer, result.Error);
    }

    [Fact]
    public async Task Connection_refused_is_no_answer()
    {
        var handler = Failing(SocketError.ConnectionRefused);
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var result = await driver.ReadAllAsync(device, CancellationToken.None);

        Assert.Equal(DeviceError.NoAnswer, result.Error);
    }

    [Fact]
    public async Task Timeout_is_no_answer()
    {
        var handler = new FakeShellyHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);
        var fiftyMilliseconds = TimeSpan.FromMilliseconds(50);
        using var timeout = new CancellationTokenSource(fiftyMilliseconds);

        var result = await driver.ReadAllAsync(device, timeout.Token);

        Assert.Equal(DeviceError.NoAnswer, result.Error);
    }

    [Fact]
    public async Task Other_network_failure_is_no_answer()
    {
        var handler = Failing(SocketError.HostUnreachable);
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var result = await driver.SetAsync(device, 1, true, CancellationToken.None);

        Assert.Equal(DeviceError.NoAnswer, result.Error);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("42")]
    public async Task Reply_that_is_not_an_object_is_an_error_answer(string json)
    {
        var handler = Answering(json);
        var driver = DriverFor(handler);
        var device = ShellyWith(channelCount: 1);

        var readAll = await driver.ReadAllAsync(device, CancellationToken.None);
        var read = await driver.ReadIsOnAsync(device, 1, CancellationToken.None);

        Assert.Equal(DeviceError.ErrorAnswer, readAll.Error);
        Assert.Equal(DeviceError.ErrorAnswer, read.Error);
    }


    private static ShellyRelayDriver DriverFor(FakeShellyHandler handler)
    {
        var http = new HttpClient(handler);
        return new ShellyRelayDriver(http);
    }

    private static Device ShellyWith(int channelCount)
    {
        return new Device("Bar", DeviceKind.ShellyGen2, "192.168.10.30", 8080, channelCount, CreatedAt);
    }

    private static FakeShellyHandler Answering(string json)
    {
        return new FakeShellyHandler((_, _) =>
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            return Task.FromResult(response);
        });
    }

    private static FakeShellyHandler Failing(SocketError error)
    {
        return new FakeShellyHandler((_, _) =>
        {
            var socketError = new SocketException((int)error);
            throw new HttpRequestException("Device call failed", socketError);
        });
    }
}
