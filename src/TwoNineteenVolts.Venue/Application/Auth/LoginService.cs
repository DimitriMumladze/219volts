using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Auth;

public class LoginService(IVenueDbContext db, IPinHasher hasher, LoginThrottle throttle, IAuditLog audit)
{
    // Unknown, inactive and Support users get the same answer as a wrong PIN.
    public async Task<LoginResult> LoginAsync(int userId, string pin)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && u.Role != Role.Support);
        if (user is null)
        {
            // Nobody can log in with this id, so there is no PIN to guess and nothing to slow down;
            // keeping it out of the throttle stops made-up ids from filling its memory.
            await RecordFailureAsync($"user:{userId}");
            return LoginResult.WrongCredentials;
        }

        return await CheckSecretAsync(user, pin);
    }

    public async Task<LoginResult> SupportLoginAsync(string password)
    {
        var support = await db.Users.SingleOrDefaultAsync(u => u.Role == Role.Support && u.IsActive);
        if (support is null)
        {
            // Only possible before setup has run; there is no password to guess.
            await RecordFailureAsync(target: null);
            return LoginResult.WrongCredentials;
        }

        return await CheckSecretAsync(support, password);
    }

    private async Task<LoginResult> CheckSecretAsync(User user, string secret)
    {
        if (throttle.TryStartAttempt(user.Id) is { } wait)
        {
            return LoginResult.Throttled(wait);
        }

        if (!hasher.Verify(user.SecretHash, secret))
        {
            await RecordFailureAsync($"user:{user.Id}");
            return LoginResult.WrongCredentials;
        }

        throttle.RecordSuccess(user.Id);
        audit.Record(new AuditActor(user.Id, user.DisplayName, AuditSource.Local), AuditActions.Login, $"user:{user.Id}");
        await db.SaveChangesAsync();
        return LoginResult.Success(user);
    }

    private async Task RecordFailureAsync(string? target)
    {
        audit.Record(AuditActor.Unknown, AuditActions.LoginFailed, target);
        await db.SaveChangesAsync();
    }
}
