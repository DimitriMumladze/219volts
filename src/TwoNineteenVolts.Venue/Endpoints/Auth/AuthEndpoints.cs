using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Auth;
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
        audit.Record(actor, AuditActions.Logout, $"user:{actor.UserId}");
        await db.SaveChangesAsync();

        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    // Read from the claims of the session: no database lookup per request.
    private static IResult GetCurrentUser(ClaimsPrincipal user)
    {
        return TypedResults.Ok(new CurrentUserResponse(
            int.Parse(user.FindFirstValue(AuthClaims.UserId)!),
            user.FindFirstValue(AuthClaims.Name)!,
            Enum.Parse<Role>(user.FindFirstValue(AuthClaims.Role)!)));
    }

    private static async Task<IResult> ToResponseAsync(LoginResult result, HttpContext http)
    {
        if (result.RetryAfter is { } wait)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            http.Response.Headers.RetryAfter = seconds.ToString();
            return TypedResults.Problem(
                title: $"Try again in {seconds} {(seconds == 1 ? "second" : "seconds")}",
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (result.User is not { } user)
        {
            return TypedResults.Problem(title: "Wrong PIN", statusCode: StatusCodes.Status401Unauthorized);
        }

        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, CreatePrincipal(user));
        return TypedResults.Ok(new CurrentUserResponse(user.Id, user.DisplayName, user.Role));
    }

    private static ClaimsPrincipal CreatePrincipal(User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(AuthClaims.UserId, user.Id.ToString()),
                new Claim(AuthClaims.Name, user.DisplayName),
                new Claim(AuthClaims.Role, user.Role.ToString()),
                new Claim(AuthClaims.Source, nameof(AuditSource.Local))
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identity);
    }
}
