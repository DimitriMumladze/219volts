using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Infrastructure.Devices;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceCheckServiceTests
{
    [Fact]
    public async Task Check_reads_all_channels_of_every_device()
    {
        using var factory = new VenueAppFactory();
        var shelly = await AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 2);
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        factory.Shelly.SetChannels(shelly, true, false);
        factory.Waveshare.SetChannels(panel, false, false, true, false, false, false, false, true);

        await CheckAsync(factory);

        var statuses = Statuses(factory);
        Assert.True(statuses.Get(shelly.Id).IsOnline);
        Assert.Equal([true, false], statuses.Get(shelly.Id).Channels);
        Assert.Equal([false, false, true, false, false, false, false, true], statuses.Get(panel.Id).Channels);
    }

    [Fact]
    public async Task Device_that_misses_three_checks_goes_offline()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        await CheckAsync(factory);
        factory.Waveshare.Error = DeviceError.NoAnswer;

        await CheckAsync(factory);
        await CheckAsync(factory);
        Assert.True(Statuses(factory).Get(panel.Id).IsOnline);

        await CheckAsync(factory);
        Assert.False(Statuses(factory).Get(panel.Id).IsOnline);
    }

    [Fact]
    public async Task Error_answer_keeps_device_online()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        factory.Waveshare.Error = DeviceError.ErrorAnswer;

        for (var check = 1; check <= 4; check++)
        {
            await CheckAsync(factory);
        }

        Assert.True(Statuses(factory).Get(panel.Id).IsOnline);
    }

    [Fact]
    public async Task Check_picks_up_a_newly_registered_device()
    {
        using var factory = new VenueAppFactory();
        await CheckAsync(factory);
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);

        await CheckAsync(factory);

        Assert.True(Statuses(factory).Get(panel.Id).IsOnline);
    }

    [Fact]
    public async Task Devices_are_checked_in_parallel()
    {
        using var factory = new VenueAppFactory();
        var shelly = await AddAsync(factory, "Bar", DeviceKind.ShellyGen2, "192.168.10.30", 80, 1);
        var panel = await AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 8);
        factory.Shelly.Gate = new TaskCompletionSource();
        factory.Waveshare.Gate = new TaskCompletionSource();

        // Both reads hold until released: a check that runs devices one after another
        // never reaches the second device while the first one is still waiting.
        var check = CheckAsync(factory);
        var bothStarted = SpinWait.SpinUntil(
            () => factory.Shelly.Calls.Contains($"read-all {shelly.Id}") && factory.Waveshare.Calls.Contains($"read-all {panel.Id}"),
            TimeSpan.FromSeconds(5));
        factory.Shelly.Gate.SetResult();
        factory.Waveshare.Gate.SetResult();
        await check;

        Assert.True(bothStarted);
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
