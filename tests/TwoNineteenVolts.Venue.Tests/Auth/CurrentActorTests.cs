using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Tests.Auth;

public class CurrentActorTests
{
    [Fact]
    public void Logged_in_user_becomes_the_actor()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, "7"),
                new Claim(AuthClaims.Name, "Nino"),
                new Claim(AuthClaims.Role, "Manager"),
                new Claim(AuthClaims.Source, "Local")
            ],
            "test");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };

        var actor = new CurrentActor(accessor).Get();

        Assert.Equal(new AuditActor(7, "Nino", AuditSource.Local), actor);
    }

    [Fact]
    public void Nobody_logged_in_gives_the_unknown_actor()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };

        var actor = new CurrentActor(accessor).Get();

        Assert.Equal(AuditActor.Unknown, actor);
    }
}
