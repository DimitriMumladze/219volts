using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Users;

public class UserService(
    IVenueDbContext db,
    IPinHasher hasher,
    IAuditLog audit,
    ICurrentActor currentActor,
    ISessionRegistry sessions,
    TimeProvider time)
{
    public async Task<Result<User>> CreateAsync(string displayName, string pin)
    {
        var name = displayName.Trim();
        if (name.Length == 0)
        {
            return Fail(UserError.InvalidName);
        }

        if (!PinFormat.IsValid(pin))
        {
            return Fail(UserError.InvalidPin);
        }

        if (await IsNameTakenAsync(name, ignoredUserId: null))
        {
            return Fail(UserError.DuplicateName);
        }

        var pinHash = hasher.Hash(pin);
        var now = time.GetUtcNow().UtcDateTime;
        var user = new User(name, Role.Staff, pinHash, now);

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var actor = currentActor.Get();
        var target = TargetOf(user);
        audit.Record(actor, AuditActions.UserCreated, target);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result<User>.Ok(user);
    }

    public async Task<Result<User>> RenameAsync(int id, string displayName)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        var name = displayName.Trim();
        if (name.Length == 0)
        {
            return Fail(UserError.InvalidName);
        }

        if (await IsNameTakenAsync(name, ignoredUserId: id))
        {
            return Fail(UserError.DuplicateName);
        }

        var oldName = user.DisplayName;
        user.Rename(name);
        var result = await SaveAsync(user, AuditActions.UserRenamed, details: oldName);

        sessions.EndSessionsFor(id);
        return result;
    }

    public async Task<Result<User>> SetPinAsync(int id, string pin)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        if (!PinFormat.IsValid(pin))
        {
            return Fail(UserError.InvalidPin);
        }

        var pinHash = hasher.Hash(pin);
        user.SetSecret(pinHash);
        var result = await SaveAsync(user, AuditActions.UserPinSet);

        sessions.EndSessionsFor(id);
        return result;
    }

    public async Task<Result<User>> DeactivateAsync(int id)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        if (user.Role == Role.Manager)
        {
            return Fail(UserError.ManagerCannotBeDeactivated);
        }

        user.Deactivate();
        var result = await SaveAsync(user, AuditActions.UserDeactivated);

        sessions.EndSessionsFor(id);
        return result;
    }

    public async Task<Result<User>> ReactivateAsync(int id)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        user.Reactivate();
        return await SaveAsync(user, AuditActions.UserReactivated);
    }


    private Task<User?> FindManageableAsync(int id) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role != Role.Support);

    private Task<bool> IsNameTakenAsync(string name, int? ignoredUserId)
    {
        var key = name.ToUpperInvariant();
        return db.Users.AnyAsync(u => u.DisplayNameKey == key && u.Id != ignoredUserId);
    }

    private async Task<Result<User>> SaveAsync(User user, string action, string? details = null)
    {
        var actor = currentActor.Get();
        var target = TargetOf(user);
        audit.Record(actor, action, target, details);
        await db.SaveChangesAsync();
        return Result<User>.Ok(user);
    }

    private static string TargetOf(User user) => AuditTarget.User(user.Id);

    private static Result<User> Fail(string error) => Result<User>.Fail(error);
}
