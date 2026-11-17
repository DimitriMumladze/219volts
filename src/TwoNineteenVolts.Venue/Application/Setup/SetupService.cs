using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Auth.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Setup.Contracts;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Setup;

public class SetupService(IVenueDbContext db, IPinHasher hasher, IAuditLog audit, TimeProvider time)
{
    public const string SupportDisplayName = "219volts Support";

    private static readonly AuditActor Actor = new(null, "setup", null);

    public Task<bool> IsDoneAsync() => db.Users.AnyAsync();

    public async Task<Result<string>> SetUpAsync(string ownerName, string pin)
    {
        if (await IsDoneAsync())
        {
            return Fail(SetupError.AlreadyDone);
        }

        var name = ownerName.Trim();
        if (name.Length == 0)
        {
            return Fail(SetupError.InvalidName);
        }

        if (string.Equals(name, SupportDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(SetupError.ReservedName);
        }

        if (!PinFormat.IsValid(pin))
        {
            return Fail(SetupError.InvalidPin);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var supportPassword = SecretGenerator.NewSupportPassword();
        var ownerPinHash = hasher.Hash(pin);
        var supportPasswordHash = hasher.Hash(supportPassword);
        var owner = new User(name, Role.Manager, ownerPinHash, now);
        var support = new User(SupportDisplayName, Role.Support, supportPasswordHash, now);

        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.AddRange(owner, support);
        await db.SaveChangesAsync();
        var ownerRole = owner.Role.ToString();
        var supportRole = support.Role.ToString();
        var ownerTarget = AuditTarget.User(owner.Id);
        var supportTarget = AuditTarget.User(support.Id);
        audit.Record(Actor, AuditActions.UserCreated, ownerTarget, ownerRole);
        audit.Record(Actor, AuditActions.UserCreated, supportTarget, supportRole);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result<string>.Ok(supportPassword);
    }


    private static Result<string> Fail(string error) => Result<string>.Fail(error);
}
