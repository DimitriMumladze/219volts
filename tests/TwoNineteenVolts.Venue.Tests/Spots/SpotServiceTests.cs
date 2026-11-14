using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Devices;

namespace TwoNineteenVolts.Venue.Tests.Spots;

public class SpotServiceTests
{
    private static readonly AuditActor Support = new(1, "219volts Support", Role.Support);
    private static readonly AuditActor Manager = new(2, "Nino", Role.Manager);
    private static readonly AuditActor Staff = new(3, "Gio", Role.Staff);

    [Fact]
    public async Task Create_generates_code_saves_and_audits()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync(" Billiard 1 ", panel, 1));

        var spot = await FindAsync(factory, result.Value!.Id);
        var codeLooksRight = Regex.IsMatch(spot.Code, "^[a-z0-9]{6}$");
        var entry = await SingleAuditAsync(factory, AuditActions.SpotCreated);
        Assert.True(codeLooksRight);
        Assert.Equal("Billiard 1", spot.Name);
        Assert.Equal(panel, spot.DeviceId);
        Assert.Equal(1, spot.Channel);
        Assert.False(spot.IsArchived);
        Assert.Equal($"spot:{spot.Id}", entry.Target);
    }

    [Fact]
    public async Task Create_without_device_makes_an_unmapped_spot()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 1", null, null));

        var spot = await FindAsync(factory, result.Value!.Id);
        Assert.False(spot.IsMapped);
        Assert.Null(spot.Channel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_refuses_empty_name(string name)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync(name, null, null));

        Assert.Equal(SpotError.InvalidName, result.Error);
    }

    [Fact]
    public async Task Create_refuses_name_over_30_characters_after_trimming()
    {
        using var factory = new VenueAppFactory();
        var thirty = new string('a', 30);
        var thirtyOne = new string('b', 31);

        var tooLong = await UseServiceAsync(factory, Support, spots => spots.CreateAsync(thirtyOne, null, null));
        var paddedThirty = await UseServiceAsync(factory, Support, spots => spots.CreateAsync($"  {thirty} ", null, null));

        Assert.Equal(SpotError.NameTooLong, tooLong.Error);
        Assert.False(paddedThirty.IsFailure);
    }

    [Fact]
    public async Task Create_refuses_duplicate_name_ignoring_case_including_archived()
    {
        using var factory = new VenueAppFactory();
        await TestSpots.AddAsync(factory, "Billiard 1", null, null, archived: true);

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("BILLIARD 1", null, null));

        Assert.Equal(SpotError.DuplicateName, result.Error);
    }

    [Fact]
    public async Task Create_refuses_half_mapping()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);

        var deviceOnly = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 1", panel, null));
        var channelOnly = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", null, 1));

        Assert.Equal(SpotError.InvalidMapping, deviceOnly.Error);
        Assert.Equal(SpotError.InvalidMapping, channelOnly.Error);
    }

    [Fact]
    public async Task Create_refuses_unknown_device()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 1", 999, 1));

        Assert.Equal(SpotError.UnknownDevice, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public async Task Create_refuses_channel_out_of_range(int channel)
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 1", panel, channel));

        Assert.Equal(SpotError.ChannelOutOfRange, result.Error);
    }

    [Fact]
    public async Task Create_refuses_channel_already_used()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", panel, 1));

        Assert.Equal(SpotError.ChannelInUse, result.Error);
    }

    [Fact]
    public async Task Create_returns_code_taken_when_generated_code_exists()
    {
        using var factory = new VenueAppFactory();
        await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var takenCode = await CodeOfAsync(factory, "Billiard 1");
        var codes = new FixedSpotCodeGenerator(takenCode);

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", null, null), codes);

        Assert.Equal(SpotError.CodeTaken, result.Error);
    }

    [Fact]
    public async Task Update_renames_remaps_and_audits_old_to_new()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var otherPanel = await TestDevices.AddAsync(factory, "Panel 2", DeviceKind.WaveshareModbusTcp, "192.168.10.22", 502, 16);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        var result = await UseServiceAsync(factory, Support, spots => spots.UpdateAsync(id, " Billiard A ", otherPanel, 3));

        var spot = await FindAsync(factory, id);
        var entry = await SingleAuditAsync(factory, AuditActions.SpotUpdated);
        Assert.False(result.IsFailure);
        Assert.Equal(("Billiard A", otherPanel, 3), (spot.Name, spot.DeviceId, spot.Channel));
        Assert.Equal($"spot:{id}", entry.Target);
        Assert.Equal($"name: Billiard 1 → Billiard A; device: {panel} → {otherPanel}; channel: 1 → 3", entry.Details);
    }

    [Fact]
    public async Task Update_can_unmap()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        await UseServiceAsync(factory, Support, spots => spots.UpdateAsync(id, "Billiard 1", null, null));

        var spot = await FindAsync(factory, id);
        var entry = await SingleAuditAsync(factory, AuditActions.SpotUpdated);
        Assert.False(spot.IsMapped);
        Assert.Equal($"device: {panel} → none; channel: 1 → none", entry.Details);
    }

    [Fact]
    public async Task Manager_can_rename()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        var result = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(id, "Billiard A", panel, 1));

        var spot = await FindAsync(factory, id);
        Assert.False(result.IsFailure);
        Assert.Equal("Billiard A", spot.Name);
    }

    [Fact]
    public async Task Manager_changing_device_or_channel_is_refused_and_nothing_saved()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        var otherChannel = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(id, "Billiard A", panel, 2));
        var unmap = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(id, "Billiard A", null, null));

        var spot = await FindAsync(factory, id);
        Assert.Equal(SpotError.OnlyNameChangeAllowed, otherChannel.Error);
        Assert.Equal(SpotError.OnlyNameChangeAllowed, unmap.Error);
        Assert.Equal(("Billiard 1", panel, 1), (spot.Name, spot.DeviceId, spot.Channel));
    }

    [Fact]
    public async Task Manager_sending_unchanged_mapping_can_rename()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 5);

        var result = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(id, "Billiard A", panel, 5));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task Staff_and_manager_cannot_see_unmapped_or_archived_spots()
    {
        using var factory = new VenueAppFactory();
        var unmapped = await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var archived = await TestSpots.AddAsync(factory, "Billiard 2", null, null, archived: true);

        var managerUnmapped = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(unmapped, "Billiard A", null, null));
        var managerArchived = await UseServiceAsync(factory, Manager, spots => spots.UpdateAsync(archived, "Billiard B", null, null));
        var staffUnmapped = await UseServiceAsync(factory, Staff, spots => spots.UpdateAsync(unmapped, "Billiard A", null, null));
        var staffArchived = await UseServiceAsync(factory, Staff, spots => spots.UpdateAsync(archived, "Billiard B", null, null));

        Assert.Equal(SpotError.NotFound, managerUnmapped.Error);
        Assert.Equal(SpotError.NotFound, managerArchived.Error);
        Assert.Equal(SpotError.NotFound, staffUnmapped.Error);
        Assert.Equal(SpotError.NotFound, staffArchived.Error);
    }

    [Fact]
    public async Task New_code_replaces_code_audits_and_frees_the_old_one()
    {
        using var factory = new VenueAppFactory();
        var id = await TestSpots.AddAsync(factory, "Billiard 1", null, null);
        var oldCode = await CodeOfAsync(factory, "Billiard 1");

        var result = await UseServiceAsync(factory, Support, spots => spots.NewCodeAsync(id));
        var reused = new FixedSpotCodeGenerator(oldCode);
        var other = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", null, null), reused);

        var newCode = await CodeOfAsync(factory, "Billiard 1");
        var entry = await SingleAuditAsync(factory, AuditActions.SpotCodeChanged);
        Assert.False(result.IsFailure);
        Assert.NotEqual(oldCode, newCode);
        Assert.False(other.IsFailure);
        Assert.Equal($"spot:{id}", entry.Target);
        Assert.Equal($"{oldCode} → {newCode}", entry.Details);
    }

    [Fact]
    public async Task Archive_clears_mapping_and_audits()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 1);

        var result = await UseServiceAsync(factory, Support, spots => spots.ArchiveAsync(id));
        var channelFreed = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", panel, 1));

        var spot = await FindAsync(factory, id);
        var entry = await SingleAuditAsync(factory, AuditActions.SpotArchived);
        Assert.False(result.IsFailure);
        Assert.True(spot.IsArchived);
        Assert.False(spot.IsMapped);
        Assert.False(channelFreed.IsFailure);
        Assert.Equal($"spot:{id}", entry.Target);
    }

    [Fact]
    public async Task Archived_spot_keeps_name_and_code_taken()
    {
        using var factory = new VenueAppFactory();
        await TestSpots.AddAsync(factory, "Billiard 1", null, null, archived: true);
        var archivedCode = await CodeOfAsync(factory, "Billiard 1");
        var sameCode = new FixedSpotCodeGenerator(archivedCode);

        var sameName = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 1", null, null));
        var codeTaken = await UseServiceAsync(factory, Support, spots => spots.CreateAsync("Billiard 2", null, null), sameCode);

        Assert.Equal(SpotError.DuplicateName, sameName.Error);
        Assert.Equal(SpotError.CodeTaken, codeTaken.Error);
    }

    [Fact]
    public async Task Changes_to_archived_spot_are_refused_for_support()
    {
        using var factory = new VenueAppFactory();
        var id = await TestSpots.AddAsync(factory, "Billiard 1", null, null, archived: true);

        var update = await UseServiceAsync(factory, Support, spots => spots.UpdateAsync(id, "Billiard A", null, null));
        var newCode = await UseServiceAsync(factory, Support, spots => spots.NewCodeAsync(id));
        var archive = await UseServiceAsync(factory, Support, spots => spots.ArchiveAsync(id));

        Assert.Equal(SpotError.Archived, update.Error);
        Assert.Equal(SpotError.Archived, newCode.Error);
        Assert.Equal(SpotError.Archived, archive.Error);
    }

    [Fact]
    public async Task Switch_sets_reads_back_stores_state_and_audits()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        MarkKnown(factory, panel);

        var on = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));
        var stateAfterOn = Statuses(factory).Get(panel).IsOn(3);
        var off = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, false));
        var stateAfterOff = Statuses(factory).Get(panel).IsOn(3);

        var actions = await AuditActionsAsync(factory);
        Assert.False(on.IsFailure);
        Assert.False(off.IsFailure);
        Assert.True(stateAfterOn);
        Assert.False(stateAfterOff);
        Assert.Equal([$"set {panel}/3 on", $"read {panel}/3", $"set {panel}/3 off", $"read {panel}/3"], factory.Waveshare.Calls);
        Assert.Equal([AuditActions.SpotSwitchedOn, AuditActions.SpotSwitchedOff], actions);
    }

    [Fact]
    public async Task Switch_of_unmapped_spot_is_refused()
    {
        using var factory = new VenueAppFactory();
        var id = await TestSpots.AddAsync(factory, "Billiard 1", null, null);

        var result = await UseServiceAsync(factory, Support, spots => spots.SwitchAsync(id, true));

        Assert.Equal(SpotError.Unmapped, result.Error);
        Assert.Empty(factory.Waveshare.Calls);
    }

    [Fact]
    public async Task Switch_with_no_answer_fails_counts_a_miss_and_audits_failure()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        MarkKnown(factory, panel);
        factory.Waveshare.Error = DeviceError.NoAnswer;

        var first = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));
        var stateAfterFirst = Statuses(factory).Get(panel).IsOn(3);
        await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));
        await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));

        var status = Statuses(factory).Get(panel);
        var failures = await AuditEntriesAsync(factory, AuditActions.SpotSwitchFailed);
        var reasons = failures.Select(e => e.Details);
        Assert.Equal(DeviceError.NoAnswer, first.Error);
        Assert.Null(stateAfterFirst);
        Assert.False(status.IsOnline);
        Assert.Equal(["no answer", "no answer", "no answer"], reasons);
        Assert.All(failures, e => Assert.Equal($"spot:{id}", e.Target));
    }

    [Fact]
    public async Task Switch_with_error_answer_fails_keeps_device_online_and_audits_failure()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        MarkKnown(factory, panel);
        factory.Waveshare.Error = DeviceError.ErrorAnswer;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));
        }

        var result = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));
        var status = Statuses(factory).Get(panel);
        var failures = await AuditEntriesAsync(factory, AuditActions.SpotSwitchFailed);
        var lastReason = failures.Last().Details;
        var state = status.IsOn(3);
        Assert.Equal(DeviceError.ErrorAnswer, result.Error);
        Assert.True(status.IsOnline);
        Assert.Null(state);
        Assert.Equal("error answer", lastReason);
    }

    [Fact]
    public async Task Read_back_failure_fails_the_switch()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        MarkKnown(factory, panel);
        factory.Waveshare.ReadError = DeviceError.NoAnswer;

        var result = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));

        var state = Statuses(factory).Get(panel).IsOn(3);
        var actions = await AuditActionsAsync(factory);
        Assert.Equal(DeviceError.NoAnswer, result.Error);
        Assert.Null(state);
        Assert.Equal([AuditActions.SpotSwitchFailed], actions);
    }

    [Fact]
    public async Task Read_back_mismatch_fails_with_real_state_stored()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        MarkKnown(factory, panel);
        factory.Waveshare.IgnoreSet = true;

        var result = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));

        var status = Statuses(factory).Get(panel);
        var failure = await SingleAuditAsync(factory, AuditActions.SpotSwitchFailed);
        var state = status.IsOn(3);
        Assert.Equal(SpotError.StateMismatch, result.Error);
        Assert.True(status.IsOnline);
        Assert.False(state);
        Assert.Equal("mismatch", failure.Details);
    }

    [Fact]
    public async Task Switch_on_offline_device_is_still_sent()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        var statuses = Statuses(factory);
        for (var miss = 1; miss <= 3; miss++)
        {
            statuses.RecordOutcome(panel, DeviceError.NoAnswer);
        }

        var result = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));

        var status = statuses.Get(panel);
        Assert.False(result.IsFailure);
        Assert.Contains($"set {panel}/3 on", factory.Waveshare.Calls);
        Assert.True(status.IsOnline);
    }

    [Fact]
    public async Task Switch_before_first_check_returns_the_read_back_state()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);

        var result = await UseServiceAsync(factory, Staff, spots => spots.SwitchAsync(id, true));

        var status = Statuses(factory).Get(panel);
        var channelThree = status.IsOn(3);
        var channelFour = status.IsOn(4);
        Assert.False(result.IsFailure);
        Assert.True(channelThree);
        Assert.Null(channelFour);
    }

    [Fact]
    public async Task Update_without_changes_records_nothing()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);

        var result = await UseServiceAsync(factory, Support, spots => spots.UpdateAsync(id, " Billiard 1 ", panel, 3));

        var actions = await AuditActionsAsync(factory);
        Assert.False(result.IsFailure);
        Assert.Empty(actions);
    }

    [Fact]
    public async Task Successful_switch_is_logged()
    {
        using var factory = new VenueAppFactory();
        var panel = await AddPanelAsync(factory);
        var id = await TestSpots.AddAsync(factory, "Billiard 1", panel, 3);
        var logger = new FakeLogger<SpotService>();
        using var scope = factory.Services.CreateScope();
        var actor = new FakeCurrentActor(Staff);
        var codes = new SpotCodeGenerator();
        var service = ActivatorUtilities.CreateInstance<SpotService>(scope.ServiceProvider, actor, codes, logger);

        await service.SwitchAsync(id, true);

        var levels = logger.Collector.GetSnapshot().Select(r => r.Level);
        Assert.Equal([LogLevel.Information], levels);
    }

    [Theory]
    [InlineData("Бильярд", "БИЛЬЯРД")]
    [InlineData("ბილიარდი", "ᲑᲘᲚᲘᲐᲠᲓᲘ")]
    public async Task Names_differing_only_in_case_in_any_language_are_duplicates(string existing, string candidate)
    {
        using var factory = new VenueAppFactory();
        await UseServiceAsync(factory, Support, spots => spots.CreateAsync(existing, null, null));

        var result = await UseServiceAsync(factory, Support, spots => spots.CreateAsync(candidate, null, null));

        Assert.Equal(SpotError.DuplicateName, result.Error);
    }


    private static async Task<Result<Spot>> UseServiceAsync(
        VenueAppFactory factory,
        AuditActor actor,
        Func<SpotService, Task<Result<Spot>>> action,
        ISpotCodeGenerator? codes = null)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(actor);
        var generator = codes ?? new SpotCodeGenerator();
        var service = ActivatorUtilities.CreateInstance<SpotService>(scope.ServiceProvider, currentActor, generator);
        return await action(service);
    }

    private static DeviceStatuses Statuses(VenueAppFactory factory)
    {
        return factory.Services.GetRequiredService<DeviceStatuses>();
    }

    private static void MarkKnown(VenueAppFactory factory, int deviceId)
    {
        var statuses = Statuses(factory);
        var allOff = new bool[16];
        statuses.RecordOutcome(deviceId, error: null);
        statuses.StoreChannels(deviceId, allOff);
    }

    private static async Task<List<string>> AuditActionsAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries
            .OrderBy(e => e.Id)
            .Select(e => e.Action)
            .ToListAsync();
    }

    private static async Task<List<AuditEntry>> AuditEntriesAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries
            .Where(e => e.Action == action)
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    private static Task<int> AddPanelAsync(VenueAppFactory factory)
    {
        return TestDevices.AddAsync(factory, "Panel 1", DeviceKind.WaveshareModbusTcp, "192.168.10.21", 502, 16);
    }

    private static async Task<Spot> FindAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Spots.SingleAsync(s => s.Id == id);
    }

    private static async Task<string> CodeOfAsync(VenueAppFactory factory, string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        var spot = await db.Spots.SingleAsync(s => s.Name == name);
        return spot.Code;
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }
}
