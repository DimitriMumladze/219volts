using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Users;

// The venue has one Manager, created by the setup command. The Manager creates and manages Staff.
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

        var user = new User(name, Role.Staff, hasher.Hash(pin), time.GetUtcNow().UtcDateTime);

        // The audit entry targets the new id, which SQLite assigns only on insert,
        // so the user and its entry are saved in two steps inside one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        audit.Record(currentActor.Get(), AuditActions.UserCreated, TargetOf(user));
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

        // The sessions carry the old name in their claims, so the user must log in again.
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

        user.SetSecret(hasher.Hash(pin));
        var result = await SaveAsync(user, AuditActions.UserPinSet);

        // A PIN is reset when it may have leaked, so whoever is signed in with it must log in again.
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

        // Without the Manager nobody could manage users any more.
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

    // The Support user is invisible here: it is "not found" for every operation.
    private Task<User?> FindManageableAsync(int id) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id && u.Role != Role.Support);

    // DisplayName uses the NOCASE collation, so this comparison ignores case.
    private Task<bool> IsNameTakenAsync(string name, int? ignoredUserId) =>
        db.Users.AnyAsync(u => u.DisplayName == name && u.Id != ignoredUserId);

    private async Task<Result<User>> SaveAsync(User user, string action, string? details = null)
    {
        audit.Record(currentActor.Get(), action, TargetOf(user), details);
        await db.SaveChangesAsync();
        return Result<User>.Ok(user);
    }

    private static string TargetOf(User user) => $"user:{user.Id}";

    private static Result<User> Fail(string error) => Result<User>.Fail(error);
}
