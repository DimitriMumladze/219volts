using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Setup;

// Replaces a lost Support password.
public class ResetSupportService(IVenueDbContext db, IPinHasher hasher, IAuditLog audit)
{
    private static readonly AuditActor Actor = new(null, "reset-support", AuditSource.Local);

    // On success the value is the new Support password, to be shown exactly once.
    public async Task<Result<string>> ResetAsync()
    {
        var support = await db.Users.SingleOrDefaultAsync(u => u.Role == Role.Support);
        if (support is null)
        {
            return Result<string>.Fail(SetupError.NoSupportUser);
        }

        var password = SecretGenerator.NewSupportPassword();
        support.SetSecret(hasher.Hash(password));
        audit.Record(Actor, AuditActions.SupportPasswordReset, $"user:{support.Id}");
        await db.SaveChangesAsync();

        return Result<string>.Ok(password);
    }
}
