using TwoNineteenVolts.Venue.Application.Audit;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class CurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public AuditActor Get()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return AuditActor.Unknown;
        }

        return new AuditActor(
            int.Parse(user.FindFirst(AuthClaims.UserId)!.Value),
            user.FindFirst(AuthClaims.Name)!.Value);
    }
}
