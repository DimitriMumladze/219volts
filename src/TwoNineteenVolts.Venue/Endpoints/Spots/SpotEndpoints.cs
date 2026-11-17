using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotEndpoints
{
    public static void MapSpotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/spots").RequireAuthorization(Policies.Staff);

        group.MapGet("/", GetSpotsAsync);
        group.MapGet("/{id:int}", GetSpotAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}", UpdateAsync).RequireAuthorization(Policies.Manager);
        group.MapPost("/{id:int}/new-code", NewCodeAsync).RequireAuthorization(Policies.Support);
        group.MapPost("/{id:int}/archive", ArchiveAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}/state", SwitchAsync);
    }


    private static async Task<IResult> GetSpotsAsync(IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings, ICurrentActor currentActor)
    {
        var actor = currentActor.Get();
        var spots = db.Spots.AsQueryable();
        if (actor.Role != Role.Support)
        {
            spots = spots.Where(s => s.DeviceId != null && !s.IsArchived);
        }

        var visible = await spots
            .OrderBy(s => s.Name)
            .ToListAsync();

        var responses = await SpotResponses.BuildAllAsync(visible, db, statuses, warnings);
        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> GetSpotAsync(
        int id,
        VisibleSpots visibleSpots,
        IVenueDbContext db,
        DeviceStatuses statuses,
        SwitchOffWarnings warnings,
        ICurrentActor currentActor)
    {
        var actor = currentActor.Get();
        var spot = await visibleSpots.FindAsync(id, actor);
        if (spot is null)
        {
            return SpotProblems.ToProblem(SpotError.NotFound);
        }

        var response = await SpotResponses.BuildAsync(spot, db, statuses, warnings);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CreateAsync(
        CreateSpotRequest request,
        SpotService spots,
        IVenueDbContext db,
        DeviceStatuses statuses,
        SwitchOffWarnings warnings)
    {
        var result = await spots.CreateAsync(request.Name, request.GroupId, request.DeviceId, request.Channel);
        if (result.IsFailure)
        {
            return SpotProblems.ToProblem(result.Error);
        }

        var spot = result.Value;
        var response = await SpotResponses.BuildAsync(spot, db, statuses, warnings);
        return TypedResults.Created($"/api/spots/{spot.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        int id,
        UpdateSpotRequest request,
        SpotService spots,
        IVenueDbContext db,
        DeviceStatuses statuses,
        SwitchOffWarnings warnings)
    {
        var result = await spots.UpdateAsync(id, request.Name, request.GroupId, request.DeviceId, request.Channel);
        return await ToResultAsync(result, db, statuses, warnings);
    }

    private static async Task<IResult> NewCodeAsync(int id, SpotService spots, IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings)
    {
        var result = await spots.NewCodeAsync(id);
        return await ToResultAsync(result, db, statuses, warnings);
    }

    private static async Task<IResult> ArchiveAsync(int id, SpotService spots, IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings)
    {
        var result = await spots.ArchiveAsync(id);
        return await ToResultAsync(result, db, statuses, warnings);
    }

    private static async Task<IResult> SwitchAsync(
        int id,
        SwitchSpotRequest request,
        SpotService spots,
        IVenueDbContext db,
        DeviceStatuses statuses,
        SwitchOffWarnings warnings)
    {
        var result = await spots.SwitchAsync(id, request.On);
        if (result.IsFailure)
        {
            return await SpotProblems.ForSpotAsync(result.Error, id, db, statuses);
        }

        return await ToResultAsync(result, db, statuses, warnings);
    }

    private static async Task<IResult> ToResultAsync(Result<Spot> result, IVenueDbContext db, DeviceStatuses statuses, SwitchOffWarnings warnings)
    {
        if (result.IsFailure)
        {
            return SpotProblems.ToProblem(result.Error);
        }

        var response = await SpotResponses.BuildAsync(result.Value, db, statuses, warnings);
        return TypedResults.Ok(response);
    }
}
