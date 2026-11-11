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
    public async Task<Result<User>> CreateAsync(string displayName, Role role, string pin)
    {
        var name = displayName.Trim();
        if (name.Length == 0)
        {
            return Fail(UserError.InvalidName);
        }

        if (!IsAssignable(role))
        {
            return Fail(UserError.InvalidRole);
        }

        if (!PinFormat.IsValid(pin))
        {
            return Fail(UserError.InvalidPin);
        }

        if (await IsNameTakenAsync(name, ignoredUserId: null))
        {
            return Fail(UserError.DuplicateName);
        }

        var user = new User(name, role, hasher.Hash(pin), time.GetUtcNow().UtcDateTime);

        // The audit entry targets the new id, which SQLite assigns only on insert,
        // so the user and its entry are saved in two steps inside one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        audit.Record(currentActor.Get(), AuditActions.UserCreated, TargetOf(user), role.ToString());
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

    public async Task<Result<User>> ChangeRoleAsync(int id, Role role)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        if (!IsAssignable(role))
        {
            return Fail(UserError.InvalidRole);
        }

        if (role == user.Role)
        {
            return Result<User>.Ok(user);
        }

        if (await IsLastActiveManagerAsync(user))
        {
            return Fail(UserError.LastManager);
        }

        var oldRole = user.Role;
        user.ChangeRole(role);
        var result = await SaveAsync(user, AuditActions.UserRoleChanged, details: $"{oldRole} → {role}");

        // The sessions carry the old role in their claims, so the user must log in again.
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
        return await SaveAsync(user, AuditActions.UserPinSet);
    }

    public async Task<Result<User>> SetRemoteAccessAsync(int id, bool allowed)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        user.SetRemoteAccess(allowed);
        return await SaveAsync(user, AuditActions.UserRemoteAccessChanged, details: allowed ? "on" : "off");
    }

    public async Task<Result<User>> DeactivateAsync(int id)
    {
        var user = await FindManageableAsync(id);
        if (user is null)
        {
            return Fail(UserError.NotFound);
        }

        if (await IsLastActiveManagerAsync(user))
        {
            return Fail(UserError.LastManager);
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

    // Support does not count as a manager.
    private async Task<bool> IsLastActiveManagerAsync(User user) =>
        user.IsActive
        && user.Role == Role.Manager
        && await db.Users.CountAsync(u => u.IsActive && u.Role == Role.Manager) == 1;

    private async Task<Result<User>> SaveAsync(User user, string action, string? details = null)
    {
        audit.Record(currentActor.Get(), action, TargetOf(user), details);
        await db.SaveChangesAsync();
        return Result<User>.Ok(user);
    }

    private static bool IsAssignable(Role role) => role is Role.Staff or Role.Manager;

    private static string TargetOf(User user) => $"user:{user.Id}";

    private static Result<User> Fail(string error) => Result<User>.Fail(error);
}
