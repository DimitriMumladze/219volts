using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Setup;

// Installs a venue: creates the owner (a Manager) and the Support user, once.
public class SetupService(IVenueDbContext db, IPinHasher hasher, IAuditLog audit, TimeProvider time)
{
    public const string SupportDisplayName = "219volts Support";

    private static readonly AuditActor Actor = new(null, "setup");

    public Task<bool> IsDoneAsync() => db.Users.AnyAsync();

    // On success the value is the new Support password, to be shown to the installer exactly once.
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
        var owner = new User(name, Role.Manager, hasher.Hash(pin), now);
        var support = new User(SupportDisplayName, Role.Support, hasher.Hash(supportPassword), now);

        // The audit entries target the new ids, which SQLite assigns only on insert,
        // so the users and their entries are saved in two steps inside one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.AddRange(owner, support);
        await db.SaveChangesAsync();
        audit.Record(Actor, AuditActions.UserCreated, $"user:{owner.Id}", owner.Role.ToString());
        audit.Record(Actor, AuditActions.UserCreated, $"user:{support.Id}", support.Role.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result<string>.Ok(supportPassword);
    }

    private static Result<string> Fail(string error) => Result<string>.Fail(error);
}
