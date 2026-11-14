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

        var idClaim = user.FindFirst(AuthClaims.UserId)!.Value;
        var name = user.FindFirst(AuthClaims.Name)!.Value;
        var id = int.Parse(idClaim);

        return new AuditActor(id, name);
    }
}
