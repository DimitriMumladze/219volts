using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Groups;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Groups;

public static class GroupEndpoints
{
    public static void MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/groups").RequireAuthorization(Policies.Staff);

        group.MapGet("/", GetGroupsAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}/name", RenameAsync).RequireAuthorization(Policies.Manager);
        group.MapPut("/{id:int}/rate", SetRateAsync).RequireAuthorization(Policies.Manager);
        group.MapPost("/{id:int}/archive", ArchiveAsync).RequireAuthorization(Policies.Support);
    }


    private static async Task<IResult> GetGroupsAsync(IVenueDbContext db, ICurrentActor currentActor)
    {
        var actor = currentActor.Get();
        var groups = db.Groups.AsQueryable();
        if (actor.Role != Role.Support)
        {
            groups = groups.Where(g => !g.IsArchived);
        }

        var visible = await groups
            .OrderBy(g => g.Name)
            .ToListAsync();

        var responses = visible.Select(ToResponse).ToList();
        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> CreateAsync(CreateGroupRequest request, GroupService groups)
    {
        var result = await groups.CreateAsync(request.Name, request.HourlyRate);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var created = result.Value;
        var response = ToResponse(created);
        return TypedResults.Created($"/api/groups/{created.Id}", response);
    }

    private static async Task<IResult> RenameAsync(int id, RenameGroupRequest request, GroupService groups)
    {
        var result = await groups.RenameAsync(id, request.Name);
        return ToResult(result);
    }

    private static async Task<IResult> SetRateAsync(int id, SetRateRequest request, GroupService groups)
    {
        var result = await groups.SetRateAsync(id, request.HourlyRate);
        return ToResult(result);
    }

    private static async Task<IResult> ArchiveAsync(int id, GroupService groups)
    {
        var result = await groups.ArchiveAsync(id);
        return ToResult(result);
    }

    private static IResult ToResult(Result<Group> result)
    {
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var response = ToResponse(result.Value);
        return TypedResults.Ok(response);
    }

    private static IResult ToProblem(string error)
    {
        if (error == GroupError.NameTooLong)
        {
            return Problems.Create(error, StatusCodes.Status400BadRequest, textArguments: [GroupService.MaxNameLength]);
        }

        var statusCode = error switch
        {
            GroupError.NotFound => StatusCodes.Status404NotFound,
            GroupError.InvalidName or GroupError.InvalidRate => StatusCodes.Status400BadRequest,
            GroupError.DuplicateName or GroupError.Archived or GroupError.HasSpots => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problems.Create(error, statusCode);
    }

    private static GroupResponse ToResponse(Group group)
    {
        var hourlyRate = Money.ToGel(group.HourlyRateTetri);
        return new GroupResponse(group.Id, group.Name, hourlyRate, group.IsArchived);
    }
}
