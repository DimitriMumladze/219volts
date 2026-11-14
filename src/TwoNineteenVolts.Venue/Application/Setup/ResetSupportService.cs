using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Setup;

public class ResetSupportService(IVenueDbContext db, IPinHasher hasher, IAuditLog audit)
{
    private static readonly AuditActor Actor = new(null, "reset-support");

    public async Task<Result<string>> ResetAsync()
    {
        var support = await db.Users.SingleOrDefaultAsync(u => u.Role == Role.Support);
        if (support is null)
        {
            return Result<string>.Fail(SetupError.NoSupportUser);
        }

        var password = SecretGenerator.NewSupportPassword();
        var passwordHash = hasher.Hash(password);
        support.SetSecret(passwordHash);
        var target = AuditTarget.User(support.Id);
        audit.Record(Actor, AuditActions.SupportPasswordReset, target);
        await db.SaveChangesAsync();

        return Result<string>.Ok(password);
    }
}
