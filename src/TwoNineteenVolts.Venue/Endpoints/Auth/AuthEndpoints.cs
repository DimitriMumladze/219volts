using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Auth;
using TwoNineteenVolts.Venue.Application.Auth.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapGet("/users", GetLoginUsersAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/support-login", SupportLoginAsync);
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", GetCurrentUser).RequireAuthorization();
    }


    private static async Task<IResult> GetLoginUsersAsync(IVenueDbContext db)
    {
        var users = await db.Users
            .Where(u => u.IsActive && u.Role != Role.Support)
            .OrderBy(u => u.DisplayName)
            .Select(u => new LoginUserResponse(u.Id, u.DisplayName))
            .ToListAsync();

        return TypedResults.Ok(users);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, LoginService login, HttpContext http)
    {
        var result = await login.LoginAsync(request.UserId, request.Pin);
        return await ToResponseAsync(result, http);
    }

    private static async Task<IResult> SupportLoginAsync(SupportLoginRequest request, LoginService login, HttpContext http)
    {
        var result = await login.SupportLoginAsync(request.Password);
        return await ToResponseAsync(result, http);
    }

    private static async Task<IResult> LogoutAsync(ICurrentActor currentActor, IAuditLog audit, IVenueDbContext db, HttpContext http)
    {
        var actor = currentActor.Get();
        var target = AuditTarget.User(actor.UserId);
        audit.Record(actor, AuditActions.Logout, target);
        await db.SaveChangesAsync();

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static IResult GetCurrentUser(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirstValue(AuthClaims.UserId)!;
        var name = user.FindFirstValue(AuthClaims.Name)!;
        var roleClaim = user.FindFirstValue(AuthClaims.Role)!;
        var id = int.Parse(idClaim);
        var role = Enum.Parse<Role>(roleClaim);

        return TypedResults.Ok(new CurrentUserResponse(id, name, role));
    }

    private static async Task<IResult> ToResponseAsync(LoginResult result, HttpContext http)
    {
        if (result.RetryAfter is TimeSpan wait)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            http.Response.Headers.RetryAfter = seconds.ToString();
            var key = seconds == 1 ? AuthError.TryAgainInOneSecond : AuthError.TryAgainInSeconds;
            return Problems.Create(key, StatusCodes.Status429TooManyRequests, textArguments: [seconds]);
        }

        var user = result.User;
        if (user is null)
        {
            return Problems.Create(AuthError.WrongPin, StatusCodes.Status401Unauthorized);
        }

        var principal = CreatePrincipal(user);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return TypedResults.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Role));
    }

    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var userId = user.Id.ToString();
        var role = user.Role.ToString();
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, userId),
                new Claim(AuthClaims.Name, user.DisplayName),
                new Claim(AuthClaims.Role, role)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}
