using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Users;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Users;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization(Policies.Manager);

        group.MapGet("/", GetUsersAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:int}/name", RenameAsync);
        group.MapPut("/{id:int}/pin", SetPinAsync);
        group.MapPost("/{id:int}/deactivate", DeactivateAsync);
        group.MapPost("/{id:int}/reactivate", ReactivateAsync);
    }

    private static async Task<IResult> GetUsersAsync(IVenueDbContext db)
    {
        var users = await db.Users
            .Where(u => u.Role != Role.Support)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        return TypedResults.Ok(users.Select(ToResponse));
    }

    private static async Task<IResult> CreateAsync(CreateUserRequest request, UserService users)
    {
        var result = await users.CreateAsync(request.DisplayName, request.Pin);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var user = result.Value!;
        return TypedResults.Created($"/api/users/{user.Id}", ToResponse(user));
    }

    private static async Task<IResult> RenameAsync(int id, RenameUserRequest request, UserService users) =>
        ToResult(await users.RenameAsync(id, request.DisplayName));

    private static async Task<IResult> SetPinAsync(int id, SetPinRequest request, UserService users) =>
        ToResult(await users.SetPinAsync(id, request.Pin));

    private static async Task<IResult> DeactivateAsync(int id, UserService users) =>
        ToResult(await users.DeactivateAsync(id));

    private static async Task<IResult> ReactivateAsync(int id, UserService users) =>
        ToResult(await users.ReactivateAsync(id));

    private static IResult ToResult(Result<User> result) =>
        result.IsFailure ? ToProblem(result.Error!) : TypedResults.Ok(ToResponse(result.Value!));

    private static IResult ToProblem(string error)
    {
        var statusCode = error switch
        {
            UserError.NotFound => StatusCodes.Status404NotFound,
            UserError.DuplicateName or UserError.ManagerCannotBeDeactivated => StatusCodes.Status409Conflict,
            UserError.InvalidPin or UserError.InvalidName => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        return TypedResults.Problem(title: error, statusCode: statusCode);
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.DisplayName, user.Role, user.IsActive, user.CreatedAt);
}
