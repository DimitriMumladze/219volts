using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;
using TwoNineteenVolts.Venue.Infrastructure.Data;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

// "TwoNineteenVolts.Venue.exe reset-support": for a lost Support password, run on the venue PC
// from an administrator command prompt. Not reachable over the network.
public class ResetSupportCommand(VenueDbContext db, IPinHasher hasher, IAuditLog audit, IConsole console, Func<bool> isAdministrator)
{
    private static readonly AuditActor Actor = new(null, "reset-support", AuditSource.Local);

    public async Task<int> RunAsync()
    {
        if (!isAdministrator())
        {
            return Fail("Run this from an administrator command prompt. Nothing was changed.");
        }

        var support = await db.Users.SingleOrDefaultAsync(u => u.Role == Role.Support);
        if (support is null)
        {
            return Fail("There is no support user yet: run setup first. Nothing was changed.");
        }

        var password = SecretGenerator.NewSupportPassword();
        support.SetSecret(hasher.Hash(password));
        audit.Record(Actor, AuditActions.SupportPasswordReset, $"user:{support.Id}");
        await db.SaveChangesAsync();

        console.WriteLine("New support password. Save it in the password manager now; it is shown only this once:");
        console.WriteLine(password);
        // Sessions live in the memory of the running service, which this separate process cannot reach.
        console.WriteLine("Then restart the 219volts service to sign out active Support sessions.");
        return 0;
    }

    private int Fail(string message)
    {
        console.WriteLine(message);
        return 1;
    }
}
