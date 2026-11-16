using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Events;

namespace TwoNineteenVolts.Venue.Tests.Devices;

public class DeviceServiceTests
{
    private const DeviceKind Shelly = DeviceKind.ShellyGen2;
    private const DeviceKind Waveshare = DeviceKind.WaveshareModbusTcp;

    [Fact]
    public async Task Register_saves_device_and_audits_it()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", Waveshare, "192.168.10.21", 502, 16));

        var device = await FindAsync(factory, result.Value!.Id);
        Assert.Equal("Panel 1", device.Name);
        Assert.Equal(Waveshare, device.Kind);
        Assert.Equal("192.168.10.21", device.IpAddress);
        Assert.Equal(502, device.Port);
        Assert.Equal(16, device.ChannelCount);
        var now = factory.Time.GetUtcNow().UtcDateTime;
        Assert.Equal(now, device.CreatedAt);
        var entry = await SingleAuditAsync(factory, AuditActions.DeviceRegistered);
        Assert.Equal($"device:{device.Id}", entry.Target);
    }

    [Theory]
    [InlineData(DeviceKind.ShellyGen2, 1, 80)]
    [InlineData(DeviceKind.WaveshareModbusTcp, 16, 502)]
    public async Task Register_uses_the_kinds_default_port_when_none_given(DeviceKind kind, int channelCount, int expectedPort)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", kind, "192.168.10.21", null, channelCount));

        Assert.Equal(expectedPort, result.Value!.Port);
    }

    [Fact]
    public async Task Names_are_trimmed_before_checking()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.10.21", 502, 16);

        var duplicate = await UseServiceAsync(factory, devices => devices.RegisterAsync("  Panel 1 ", Waveshare, "192.168.10.22", 502, 16));
        var created = await UseServiceAsync(factory, devices => devices.RegisterAsync("  Panel 2 ", Waveshare, "192.168.10.22", 502, 16));

        Assert.Equal(DeviceError.DuplicateName, duplicate.Error);
        Assert.Equal("Panel 2", created.Value!.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_refuses_empty_name(string name)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync(name, Waveshare, "192.168.10.21", 502, 16));

        Assert.Equal(DeviceError.InvalidName, result.Error);
    }

    [Fact]
    public async Task Register_refuses_duplicate_name_ignoring_case()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.10.21", 502, 16);

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("PANEL 1", Waveshare, "192.168.10.22", 502, 16));

        Assert.Equal(DeviceError.DuplicateName, result.Error);
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("300.1.1.1")]
    [InlineData("192.168.1")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task Register_refuses_invalid_ip_address(string ipAddress)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", Waveshare, ipAddress, 502, 16));

        Assert.Equal(DeviceError.InvalidIpAddress, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task Register_refuses_port_outside_1_to_65535(int port)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", Waveshare, "192.168.10.21", port, 16));

        Assert.Equal(DeviceError.InvalidPort, result.Error);
    }

    [Theory]
    [InlineData(DeviceKind.ShellyGen2, 0)]
    [InlineData(DeviceKind.ShellyGen2, 5)]
    [InlineData(DeviceKind.WaveshareModbusTcp, 4)]
    [InlineData(DeviceKind.WaveshareModbusTcp, 12)]
    public async Task Register_refuses_channel_count_not_allowed_for_the_kind(DeviceKind kind, int channelCount)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", kind, "192.168.10.21", null, channelCount));

        Assert.Equal(DeviceError.InvalidChannelCount, result.Error);
    }

    [Fact]
    public async Task Register_refuses_duplicate_ip_and_port()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.10.21", 502, 16);

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 2", Waveshare, "192.168.10.21", 502, 16));

        Assert.Equal(DeviceError.DuplicateAddress, result.Error);
    }

    [Fact]
    public async Task Same_ip_with_another_port_is_allowed()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.10.21", 502, 16);

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 2", Waveshare, "192.168.10.21", 503, 16));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task New_device_starts_offline_with_unknown_states()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", Shelly, "192.168.10.30", null, 4));

        var status = Statuses(factory).Get(result.Value!.Id);
        Assert.Equal(DeviceStatus.Unknown, status);
    }

    [Fact]
    public async Task Update_changes_name_ip_and_port_and_audits_old_to_new()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);

        var result = await UseServiceAsync(factory, devices => devices.UpdateAsync(id, " Panel A ", "192.168.1.21", 503));

        Assert.False(result.IsFailure);
        var device = await FindAsync(factory, id);
        Assert.Equal("Panel A", device.Name);
        Assert.Equal("192.168.1.21", device.IpAddress);
        Assert.Equal(503, device.Port);
        var entry = await SingleAuditAsync(factory, AuditActions.DeviceUpdated);
        Assert.Equal($"device:{id}", entry.Target);
        Assert.Equal("name: Panel 1 → Panel A; ip: 192.168.1.20 → 192.168.1.21; port: 502 → 503", entry.Details);
    }

    [Fact]
    public async Task Update_of_ip_or_port_starts_the_status_over()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);
        var statuses = Statuses(factory);
        statuses.RecordOutcome(id, error: null);

        await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel A", "192.168.1.20", 502));
        var afterRename = statuses.Get(id);
        Assert.True(afterRename.IsOnline);

        await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel A", "192.168.1.21", 502));
        var afterIpChange = statuses.Get(id);
        Assert.Equal(DeviceStatus.Unknown, afterIpChange);

        statuses.RecordOutcome(id, error: null);
        await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel A", "192.168.1.21", 503));
        var afterPortChange = statuses.Get(id);
        Assert.Equal(DeviceStatus.Unknown, afterPortChange);
    }

    [Fact]
    public async Task Update_refuses_duplicate_name_and_address()
    {
        using var factory = new VenueAppFactory();
        await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);
        var id = await TestDevices.AddAsync(factory, "Panel 2", Waveshare, "192.168.1.21", 502, 16);

        var name = await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "panel 1", "192.168.1.21", 502));
        var address = await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel 2", "192.168.1.20", 502));
        var unchanged = await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel 2", "192.168.1.21", 502));

        Assert.Equal(DeviceError.DuplicateName, name.Error);
        Assert.Equal(DeviceError.DuplicateAddress, address.Error);
        Assert.False(unchanged.IsFailure);
    }

    [Fact]
    public async Task Update_and_delete_of_unknown_device_are_not_found()
    {
        using var factory = new VenueAppFactory();

        var update = await UseServiceAsync(factory, devices => devices.UpdateAsync(999, "Panel 1", "192.168.1.20", 502));
        var delete = await UseServiceAsync(factory, devices => devices.DeleteAsync(999));

        Assert.Equal(DeviceError.NotFound, update.Error);
        Assert.Equal(DeviceError.NotFound, delete.Error);
    }

    [Fact]
    public async Task Delete_removes_device_unmaps_its_spots_and_lists_them_in_audit()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);
        var otherId = await TestDevices.AddAsync(factory, "Panel 2", Waveshare, "192.168.1.21", 502, 16);
        var billiard = await AddSpotAsync(factory, "aaaaaa", "Billiard 1", id, 1);
        var tennis = await AddSpotAsync(factory, "bbbbbb", "Tennis 1", id, 2);
        var other = await AddSpotAsync(factory, "cccccc", "PC 1", otherId, 1);
        Statuses(factory).RecordOutcome(id, error: null);

        var result = await UseServiceAsync(factory, devices => devices.DeleteAsync(id));

        Assert.False(result.IsFailure);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var deviceExists = await db.Devices.AnyAsync(d => d.Id == id);
        var spots = await db.Spots.OrderBy(s => s.Id).ToListAsync();
        var spotsOfDeletedDevice = spots.Where(s => s.Id != other).ToList();
        var spotOfOtherDevice = spots.Single(s => s.Id == other);
        var status = Statuses(factory).Get(id);
        Assert.False(deviceExists);
        Assert.All(spotsOfDeletedDevice, s => Assert.False(s.IsMapped));
        Assert.All(spotsOfDeletedDevice, s => Assert.Null(s.Channel));
        Assert.True(spotOfOtherDevice.IsMapped);
        Assert.Equal(DeviceStatus.Unknown, status);
        var entry = await SingleAuditAsync(factory, AuditActions.DeviceDeleted);
        Assert.Equal($"device:{id}", entry.Target);
        Assert.Equal($"unmapped spots: spot:{billiard}, spot:{tennis}", entry.Details);
    }

    [Fact]
    public async Task Update_without_changes_records_nothing()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);

        var result = await UseServiceAsync(factory, devices => devices.UpdateAsync(id, " Panel 1 ", "192.168.1.20", 502));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var auditCount = await db.AuditEntries.CountAsync();
        Assert.False(result.IsFailure);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task Delete_without_spots_says_none()
    {
        using var factory = new VenueAppFactory();
        var id = await TestDevices.AddAsync(factory, "Panel 1", Waveshare, "192.168.1.20", 502, 16);

        await UseServiceAsync(factory, devices => devices.DeleteAsync(id));

        var entry = await SingleAuditAsync(factory, AuditActions.DeviceDeleted);
        Assert.Equal("unmapped spots: none", entry.Details);
    }

    [Theory]
    [InlineData("Панель", "ПАНЕЛЬ")]
    [InlineData("პანელი", "ᲞᲐᲜᲔᲚᲘ")]
    public async Task Names_differing_only_in_case_in_any_language_are_duplicates(string existing, string candidate)
    {
        using var factory = new VenueAppFactory();
        await UseServiceAsync(factory, devices => devices.RegisterAsync(existing, Waveshare, "192.168.1.20", 502, 16));

        var result = await UseServiceAsync(factory, devices => devices.RegisterAsync(candidate, Waveshare, "192.168.1.21", 502, 16));

        Assert.Equal(DeviceError.DuplicateName, result.Error);
    }

    [Fact]
    public async Task Register_update_delete_publish_device_events()
    {
        using var factory = new VenueAppFactory();
        var registered = await UseServiceAsync(factory, devices => devices.RegisterAsync("Panel 1", Waveshare, "192.168.1.20", 502, 16));
        var id = registered.Value!.Id;
        var spot = await AddSpotAsync(factory, "k7m2qx", "Billiard 1", id, 1);
        var afterRegister = PublishedEvents.Take(factory);

        await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel 1", "192.168.1.20", 502));
        var afterNoChange = PublishedEvents.Take(factory);
        await UseServiceAsync(factory, devices => devices.UpdateAsync(id, "Panel A", "192.168.1.20", 502));
        var afterUpdate = PublishedEvents.Take(factory);
        await UseServiceAsync(factory, devices => devices.DeleteAsync(id));
        var afterDelete = PublishedEvents.Take(factory);

        Assert.Equal([new DeviceRegistered(id)], afterRegister);
        Assert.Empty(afterNoChange);
        Assert.Equal([new DeviceUpdated(id)], afterUpdate);
        var single = Assert.Single(afterDelete);
        var deleted = Assert.IsType<DeviceDeleted>(single);
        Assert.Equal(id, deleted.DeviceId);
        Assert.Equal([spot], deleted.UnmappedSpotIds);
    }

    [Fact]
    public async Task Failed_register_publishes_nothing()
    {
        using var factory = new VenueAppFactory();

        await UseServiceAsync(factory, devices => devices.RegisterAsync("", Waveshare, "192.168.1.20", 502, 16));

        var events = PublishedEvents.Take(factory);
        Assert.Empty(events);
    }


    private static async Task<Result<Device>> UseServiceAsync(VenueAppFactory factory, Func<DeviceService, Task<Result<Device>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<DeviceService>();
        return await action(service);
    }

    private static DeviceStatuses Statuses(VenueAppFactory factory) =>
        factory.Services.GetRequiredService<DeviceStatuses>();

    private static async Task<Device> FindAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Devices.SingleAsync(d => d.Id == id);
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }

    private static async Task<int> AddSpotAsync(VenueAppFactory factory, string code, string name, int deviceId, int channel)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var now = factory.Time.GetUtcNow().UtcDateTime;
        var spot = new Spot(code, name, deviceId, channel, now);
        db.Spots.Add(spot);
        await db.SaveChangesAsync();
        return spot.Id;
    }
}
