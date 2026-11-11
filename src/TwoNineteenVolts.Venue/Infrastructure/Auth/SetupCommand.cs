using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// "TwoNineteenVolts.Venue.exe setup": run once by our team when installing a venue.
public class SetupCommand(VenueDbContext db, IPinHasher hasher, IAuditLog audit, IConsole console, TimeProvider time)
{
    public const string SupportDisplayName = "219volts Support";

    private static readonly AuditActor Actor = new(null, "setup", AuditSource.Local);

    public async Task<int> RunAsync()
    {
        if (await db.Users.AnyAsync())
        {
            return Fail("Setup has already been done: users exist. Nothing was changed.");
        }

        console.WriteLine("Display name of the owner:");
        var name = console.ReadLine().Trim();
        if (name.Length == 0)
        {
            return Fail("The name must not be empty. Nothing was changed.");
        }

        if (string.Equals(name, SupportDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            return Fail($"\"{SupportDisplayName}\" is reserved. Nothing was changed.");
        }

        console.WriteLine("PIN for the owner (4 digits):");
        var pin = console.ReadSecret();
        console.WriteLine("Same PIN again:");
        var pinAgain = console.ReadSecret();
        if (pin != pinAgain)
        {
            return Fail("The PINs do not match. Nothing was changed.");
        }

        if (!PinFormat.IsValid(pin))
        {
            return Fail("The PIN must be exactly 4 digits. Nothing was changed.");
        }

        var now = time.GetUtcNow().UtcDateTime;
        var supportPassword = SecretGenerator.NewSupportPassword();
        var owner = new User(name, Role.Manager, hasher.Hash(pin), now);
        var support = new User(SupportDisplayName, Role.Support, hasher.Hash(supportPassword), now);

        // The audit entries target the new ids, which SQLite assigns only on insert.
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Users.AddRange(owner, support);
        await db.SaveChangesAsync();
        audit.Record(Actor, AuditActions.UserCreated, $"user:{owner.Id}", owner.Role.ToString());
        audit.Record(Actor, AuditActions.UserCreated, $"user:{support.Id}", support.Role.ToString());
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        console.WriteLine($"Created manager \"{owner.DisplayName}\" and the support user.");
        console.WriteLine("Support password. Save it in the password manager now; it is shown only this once:");
        console.WriteLine(supportPassword);
        return 0;
    }

    private int Fail(string message)
    {
        console.WriteLine(message);
        return 1;
    }
}
