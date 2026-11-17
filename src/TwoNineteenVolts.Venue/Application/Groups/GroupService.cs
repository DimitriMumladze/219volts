using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Events;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Groups;

namespace TwoNineteenVolts.Venue.Application.Groups;

public class GroupService(
    IVenueDbContext db,
    IAuditLog audit,
    ICurrentActor currentActor,
    IEventQueue events,
    TimeProvider time)
{
    public const int MaxNameLength = 30;

    private const decimal MaxRate = 9999.99m;

    public async Task<Result<Group>> CreateAsync(string name, decimal hourlyRate)
    {
        var cleanName = name.Trim();
        var error = ValidateName(cleanName);
        if (error is not null)
        {
            return Fail(error);
        }

        error = ValidateRate(hourlyRate);
        if (error is not null)
        {
            return Fail(error);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await IsNameTakenAsync(cleanName, ignoredGroupId: null))
        {
            return Fail(GroupError.DuplicateName);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var rateTetri = Money.ToTetri(hourlyRate);
        var group = new Group(cleanName, rateTetri, now);
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        var actor = currentActor.Get();
        var target = AuditTarget.Group(group.Id);
        audit.Record(actor, AuditActions.GroupCreated, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        events.Publish(new GroupCreated(group.Id));
        return Result<Group>.Ok(group);
    }

    public async Task<Result<Group>> RenameAsync(int id, string name)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == id);
        if (group is null)
        {
            return Fail(GroupError.NotFound);
        }

        if (group.IsArchived)
        {
            return Fail(GroupError.Archived);
        }

        var cleanName = name.Trim();
        var error = ValidateName(cleanName);
        if (error is not null)
        {
            return Fail(error);
        }

        if (group.Name == cleanName)
        {
            return Result<Group>.Ok(group);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await IsNameTakenAsync(cleanName, ignoredGroupId: id))
        {
            return Fail(GroupError.DuplicateName);
        }

        var oldName = group.Name;
        group.Rename(cleanName);
        var actor = currentActor.Get();
        var target = AuditTarget.Group(id);
        var details = AuditChanges.Change(oldName, cleanName);
        audit.Record(actor, AuditActions.GroupRenamed, target, details);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        events.Publish(new GroupRenamed(id));
        return Result<Group>.Ok(group);
    }

    public async Task<Result<Group>> SetRateAsync(int id, decimal hourlyRate)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == id);
        if (group is null)
        {
            return Fail(GroupError.NotFound);
        }

        if (group.IsArchived)
        {
            return Fail(GroupError.Archived);
        }

        var error = ValidateRate(hourlyRate);
        if (error is not null)
        {
            return Fail(error);
        }

        var rateTetri = Money.ToTetri(hourlyRate);
        if (group.HourlyRateTetri == rateTetri)
        {
            return Result<Group>.Ok(group);
        }

        var oldRate = Money.Show(group.HourlyRateTetri);
        var newRate = Money.Show(rateTetri);
        group.SetRate(rateTetri);
        var actor = currentActor.Get();
        var target = AuditTarget.Group(id);
        var details = AuditChanges.Change(oldRate, newRate);
        audit.Record(actor, AuditActions.GroupRateChanged, target, details);
        await db.SaveChangesAsync();

        events.Publish(new GroupRateChanged(id));
        return Result<Group>.Ok(group);
    }

    public async Task<Result<Group>> ArchiveAsync(int id)
    {
        var group = await db.Groups.SingleOrDefaultAsync(g => g.Id == id);
        if (group is null)
        {
            return Fail(GroupError.NotFound);
        }

        if (group.IsArchived)
        {
            return Fail(GroupError.Archived);
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Spots.AnyAsync(s => s.GroupId == id && !s.IsArchived))
        {
            return Fail(GroupError.HasSpots);
        }

        group.Archive();
        var actor = currentActor.Get();
        var target = AuditTarget.Group(id);
        audit.Record(actor, AuditActions.GroupArchived, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        events.Publish(new GroupArchived(id));
        return Result<Group>.Ok(group);
    }


    private static string? ValidateName(string name)
    {
        if (name.Length == 0)
        {
            return GroupError.InvalidName;
        }

        if (name.Length > MaxNameLength)
        {
            return GroupError.NameTooLong;
        }

        return null;
    }

    private static string? ValidateRate(decimal hourlyRate)
    {
        if (hourlyRate <= 0 || hourlyRate > MaxRate)
        {
            return GroupError.InvalidRate;
        }

        if (!Money.HasAtMostTwoDecimals(hourlyRate))
        {
            return GroupError.InvalidRate;
        }

        return null;
    }

    private Task<bool> IsNameTakenAsync(string name, int? ignoredGroupId)
    {
        var key = name.ToUpperInvariant();
        return db.Groups.AnyAsync(g => g.NameKey == key && g.Id != ignoredGroupId);
    }

    private static Result<Group> Fail(string error)
    {
        return Result<Group>.Fail(error);
    }
}
