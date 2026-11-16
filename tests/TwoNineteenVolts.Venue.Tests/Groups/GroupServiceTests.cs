using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Groups;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Data;
using TwoNineteenVolts.Venue.Tests.Events;
using TwoNineteenVolts.Venue.Tests.Spots;

namespace TwoNineteenVolts.Venue.Tests.Groups;

public class GroupServiceTests
{
    private static readonly AuditActor Support = new(1, "219volts Support", Role.Support);
    private static readonly AuditActor Manager = new(2, "Nino", Role.Manager);

    [Fact]
    public async Task Create_trims_saves_audits_and_publishes()
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, Support, groups => groups.CreateAsync(" Billiards ", 10.00m));

        var group = await FindAsync(factory, result.Value!.Id);
        var entry = await SingleAuditAsync(factory, AuditActions.GroupCreated);
        var events = PublishedEvents.Take(factory);
        Assert.Equal("Billiards", group.Name);
        Assert.Equal(1000, group.HourlyRateTetri);
        Assert.False(group.IsArchived);
        Assert.Equal($"group:{group.Id}", entry.Target);
        Assert.Equal([new GroupCreated(group.Id)], events);
    }

    [Fact]
    public async Task Rename_audits_old_to_new_and_publishes()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");

        var result = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(id, "Pool"));

        var entry = await SingleAuditAsync(factory, AuditActions.GroupRenamed);
        var events = PublishedEvents.Take(factory);
        Assert.Equal("Pool", result.Value!.Name);
        Assert.Equal("Billiards → Pool", entry.Details);
        Assert.Equal([new GroupRenamed(id)], events);
    }

    [Fact]
    public async Task Set_rate_audits_old_to_new_and_publishes()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", 1000);

        var result = await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(id, 12.5m));

        var entry = await SingleAuditAsync(factory, AuditActions.GroupRateChanged);
        var events = PublishedEvents.Take(factory);
        Assert.Equal(1250, result.Value!.HourlyRateTetri);
        Assert.Equal("10.00 → 12.50", entry.Details);
        Assert.Equal([new GroupRateChanged(id)], events);
    }

    [Fact]
    public async Task Rate_change_leaves_everything_else_alone()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", 1000);
        var other = await TestGroups.AddAsync(factory, "Pool", 2000);

        await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(id, 12m));

        var changed = await FindAsync(factory, id);
        var untouched = await FindAsync(factory, other);
        Assert.Equal("Billiards", changed.Name);
        Assert.False(changed.IsArchived);
        Assert.Equal("Pool", untouched.Name);
        Assert.Equal(2000, untouched.HourlyRateTetri);
    }

    [Fact]
    public async Task Archive_audits_and_publishes()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");

        var result = await UseServiceAsync(factory, Support, groups => groups.ArchiveAsync(id));

        var entry = await SingleAuditAsync(factory, AuditActions.GroupArchived);
        var events = PublishedEvents.Take(factory);
        Assert.True(result.Value!.IsArchived);
        Assert.Equal($"group:{id}", entry.Target);
        Assert.Equal([new GroupArchived(id)], events);
    }

    [Fact]
    public async Task Same_name_or_rate_again_records_nothing()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", 1000);

        var rename = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(id, " Billiards "));
        var rate = await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(id, 10m));

        var auditCount = await AuditCountAsync(factory);
        var events = PublishedEvents.Take(factory);
        Assert.False(rename.IsFailure);
        Assert.False(rate.IsFailure);
        Assert.Equal(0, auditCount);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData("Billiards", "BILLIARDS")]
    [InlineData("Бильярд", "БИЛЬЯРД")]
    [InlineData("ბილიარდი", "ᲑᲘᲚᲘᲐᲠᲓᲘ")]
    public async Task Names_differing_only_in_case_in_any_language_are_duplicates(string existing, string candidate)
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, existing);

        var result = await UseServiceAsync(factory, Support, groups => groups.CreateAsync(candidate, 10m));

        Assert.Equal(GroupError.DuplicateName, result.Error);
    }

    [Theory]
    [InlineData("Pool")]
    [InlineData("POOL")]
    public async Task Rename_to_another_groups_name_in_any_case_is_refused(string candidate)
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Pool");
        var id = await TestGroups.AddAsync(factory, "Billiards");

        var result = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(id, candidate));

        var group = await FindAsync(factory, id);
        Assert.Equal(GroupError.DuplicateName, result.Error);
        Assert.Equal("Billiards", group.Name);
    }

    [Fact]
    public async Task Case_only_rename_is_saved_and_audited()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Pool");

        var result = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(id, "POOL"));

        var group = await FindAsync(factory, id);
        var entry = await SingleAuditAsync(factory, AuditActions.GroupRenamed);
        Assert.False(result.IsFailure);
        Assert.Equal("POOL", group.Name);
        Assert.Equal("Pool → POOL", entry.Details);
    }

    [Fact]
    public async Task Archived_groups_keep_their_name()
    {
        using var factory = new VenueAppFactory();
        await TestGroups.AddAsync(factory, "Billiards", archived: true);

        var result = await UseServiceAsync(factory, Support, groups => groups.CreateAsync("Billiards", 10m));

        Assert.Equal(GroupError.DuplicateName, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_name_is_refused(string name)
    {
        using var factory = new VenueAppFactory();

        var result = await UseServiceAsync(factory, Support, groups => groups.CreateAsync(name, 10m));

        Assert.Equal(GroupError.InvalidName, result.Error);
    }

    [Fact]
    public async Task Name_over_30_characters_is_refused()
    {
        using var factory = new VenueAppFactory();
        var thirtyOne = new string('a', 31);
        var thirty = new string('b', 30);

        var tooLong = await UseServiceAsync(factory, Support, groups => groups.CreateAsync(thirtyOne, 10m));
        var justRight = await UseServiceAsync(factory, Support, groups => groups.CreateAsync(thirty, 10m));

        Assert.Equal(GroupError.NameTooLong, tooLong.Error);
        Assert.False(justRight.IsFailure);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10000")]
    [InlineData("1.005")]
    public async Task Invalid_rates_are_refused(string rateText)
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        var rate = decimal.Parse(rateText, System.Globalization.CultureInfo.InvariantCulture);

        var create = await UseServiceAsync(factory, Support, groups => groups.CreateAsync("Pool", rate));
        var change = await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(id, rate));

        Assert.Equal(GroupError.InvalidRate, create.Error);
        Assert.Equal(GroupError.InvalidRate, change.Error);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("9999.99")]
    public async Task Edge_rates_are_accepted(string rateText)
    {
        using var factory = new VenueAppFactory();
        var rate = decimal.Parse(rateText, System.Globalization.CultureInfo.InvariantCulture);

        var result = await UseServiceAsync(factory, Support, groups => groups.CreateAsync("Pool", rate));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task Group_with_an_active_spot_cannot_be_archived()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        await TestSpots.AddAsync(factory, "Billiard 1", null, null, groupId: id);

        var result = await UseServiceAsync(factory, Support, groups => groups.ArchiveAsync(id));

        Assert.Equal(GroupError.HasSpots, result.Error);
    }

    [Fact]
    public async Task Group_with_only_archived_spots_can_be_archived()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards");
        await TestSpots.AddAsync(factory, "Old table", null, null, archived: true, groupId: id);

        var result = await UseServiceAsync(factory, Support, groups => groups.ArchiveAsync(id));

        Assert.False(result.IsFailure);
    }

    [Fact]
    public async Task Archived_group_cannot_be_changed()
    {
        using var factory = new VenueAppFactory();
        var id = await TestGroups.AddAsync(factory, "Billiards", archived: true);

        var rename = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(id, "Pool"));
        var rate = await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(id, 12m));
        var archive = await UseServiceAsync(factory, Support, groups => groups.ArchiveAsync(id));

        Assert.Equal(GroupError.Archived, rename.Error);
        Assert.Equal(GroupError.Archived, rate.Error);
        Assert.Equal(GroupError.Archived, archive.Error);
    }

    [Fact]
    public async Task Unknown_group_is_not_found()
    {
        using var factory = new VenueAppFactory();

        var rename = await UseServiceAsync(factory, Manager, groups => groups.RenameAsync(99, "Pool"));
        var rate = await UseServiceAsync(factory, Manager, groups => groups.SetRateAsync(99, 12m));
        var archive = await UseServiceAsync(factory, Support, groups => groups.ArchiveAsync(99));

        Assert.Equal(GroupError.NotFound, rename.Error);
        Assert.Equal(GroupError.NotFound, rate.Error);
        Assert.Equal(GroupError.NotFound, archive.Error);
    }


    private static async Task<Result<Group>> UseServiceAsync(VenueAppFactory factory, AuditActor actor, Func<GroupService, Task<Result<Group>>> action)
    {
        using var scope = factory.Services.CreateScope();
        var currentActor = new FakeCurrentActor(actor);
        var service = ActivatorUtilities.CreateInstance<GroupService>(scope.ServiceProvider, currentActor);
        return await action(service);
    }

    private static async Task<Group> FindAsync(VenueAppFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.Groups.SingleAsync(g => g.Id == id);
    }

    private static async Task<AuditEntry> SingleAuditAsync(VenueAppFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.SingleAsync(e => e.Action == action);
    }

    private static async Task<int> AuditCountAsync(VenueAppFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VenueDbContext>();
        return await db.AuditEntries.CountAsync();
    }
}
