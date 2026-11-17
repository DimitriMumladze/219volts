using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Auth.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Auth;

public class LoginService(IVenueDbContext db, IPinHasher hasher, LoginThrottle throttle, IAuditLog audit)
{
    public async Task<LoginResult> LoginAsync(int userId, string pin)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive && u.Role != Role.Support);
        if (user is null)
        {
            var target = AuditTarget.User(userId);
            await RecordFailureAsync(target);
            return LoginResult.WrongCredentials;
        }

        return await CheckSecretAsync(user, pin);
    }

    public async Task<LoginResult> SupportLoginAsync(string password)
    {
        var support = await db.Users.SingleOrDefaultAsync(u => u.Role == Role.Support && u.IsActive);
        if (support is null)
        {
            await RecordFailureAsync(target: null);
            return LoginResult.WrongCredentials;
        }

        return await CheckSecretAsync(support, password);
    }


    private async Task<LoginResult> CheckSecretAsync(User user, string secret)
    {
        var isAttemptAllowed = throttle.TryStartAttempt(user.Id, out var retryAfter);
        if (!isAttemptAllowed)
        {
            return LoginResult.Throttled(retryAfter);
        }

        var isSecretRight = hasher.IsMatch(user.SecretHash, secret);
        if (!isSecretRight)
        {
            var target = AuditTarget.User(user.Id);
            await RecordFailureAsync(target);
            return LoginResult.WrongCredentials;
        }

        throttle.RecordSuccess(user.Id);
        var actor = new AuditActor(user.Id, user.DisplayName, user.Role);
        var loginTarget = AuditTarget.User(user.Id);
        audit.Record(actor, AuditActions.Login, loginTarget);
        await db.SaveChangesAsync();
        return LoginResult.Success(user);
    }

    private async Task RecordFailureAsync(string? target)
    {
        audit.Record(AuditActor.Unknown, AuditActions.LoginFailed, target);
        await db.SaveChangesAsync();
    }
}
