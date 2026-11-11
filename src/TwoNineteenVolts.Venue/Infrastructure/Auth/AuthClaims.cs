using System.Security.Claims;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public static class AuthClaims
{
    public const string UserId = ClaimTypes.NameIdentifier;
    public const string Name = ClaimTypes.Name;
    public const string Role = ClaimTypes.Role;
    public const string Source = "source";
}
