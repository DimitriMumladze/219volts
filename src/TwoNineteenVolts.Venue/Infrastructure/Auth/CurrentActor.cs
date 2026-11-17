using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Domain.Users;

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
        var roleClaim = user.FindFirst(AuthClaims.Role)!.Value;
        var id = int.Parse(idClaim);
        var role = Enum.Parse<Role>(roleClaim);

        return new AuditActor(id, name, role);
    }
}
