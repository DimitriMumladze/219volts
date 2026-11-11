using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Application.Auth;

public class LoginService(IVenueDbContext db, IPinHasher hasher, LoginThrottle throttle, IAuditLog audit)
{
    // Unknown, inactive and Support users get the same answer as a wrong PIN.
    public async Task<LoginResult> LoginAsync(int userId, string pin)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && u.Role != Role.Support);
        return await CheckSecretAsync(userId, user, pin);
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

        return await CheckSecretAsync(support.Id, support, password);
    }

    private async Task<LoginResult> CheckSecretAsync(int attemptedUserId, User? user, string secret)
    {
        if (throttle.TryStartAttempt(attemptedUserId) is { } wait)
        {
            return LoginResult.Throttled(wait);
        }

        if (user is null || !hasher.Verify(user.SecretHash, secret))
        {
            await RecordFailureAsync($"user:{attemptedUserId}");
            return LoginResult.WrongCredentials;
        }

        throttle.RecordSuccess(attemptedUserId);
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
