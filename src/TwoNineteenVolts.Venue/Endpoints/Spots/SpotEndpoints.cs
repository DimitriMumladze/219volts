using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Application.Spots.Contracts;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Spots;
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

    public static async Task<IResult> ViewResultAsync(int id, SpotReader reader, ICurrentActor currentActor)
    {
        var actor = currentActor.Get();
        var view = await reader.ViewAsync(id, actor);
        if (view is null)
        {
            return SpotProblems.ToProblem(SpotError.NotFound);
        }

        var response = SpotResponse.From(view);
        return TypedResults.Ok(response);
    }


    private static async Task<IResult> GetSpotsAsync(SpotReader reader, ICurrentActor currentActor)
    {
        var actor = currentActor.Get();
        var views = await reader.ViewAllAsync(actor);
        var responses = views.Select(SpotResponse.From).ToList();
        return TypedResults.Ok(responses);
    }

    private static Task<IResult> GetSpotAsync(int id, SpotReader reader, ICurrentActor currentActor)
    {
        return ViewResultAsync(id, reader, currentActor);
    }

    private static async Task<IResult> CreateAsync(CreateSpotRequest request, SpotService spots, SpotReader reader, ICurrentActor currentActor)
    {
        var result = await spots.CreateAsync(request.Name, request.GroupId, request.DeviceId, request.Channel);
        if (result.IsFailure)
        {
            return SpotProblems.ToProblem(result.Error);
        }

        var actor = currentActor.Get();
        var spot = result.Value;
        var view = await reader.ViewAsync(spot.Id, actor);
        if (view is null)
        {
            return SpotProblems.ToProblem(SpotError.NotFound);
        }

        var response = SpotResponse.From(view);
        return TypedResults.Created($"/api/spots/{spot.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateSpotRequest request, SpotService spots, SpotReader reader, ICurrentActor currentActor)
    {
        var result = await spots.UpdateAsync(id, request.Name, request.GroupId, request.DeviceId, request.Channel);
        return await ToResultAsync(result, reader, currentActor);
    }

    private static async Task<IResult> NewCodeAsync(int id, SpotService spots, SpotReader reader, ICurrentActor currentActor)
    {
        var result = await spots.NewCodeAsync(id);
        return await ToResultAsync(result, reader, currentActor);
    }

    private static async Task<IResult> ArchiveAsync(int id, SpotService spots, SpotReader reader, ICurrentActor currentActor)
    {
        var result = await spots.ArchiveAsync(id);
        return await ToResultAsync(result, reader, currentActor);
    }

    private static async Task<IResult> SwitchAsync(
        int id,
        SwitchSpotRequest request,
        SpotService spots,
        SpotReader reader,
        ICurrentActor currentActor,
        IVenueDbContext db,
        IDeviceGateway devices)
    {
        var result = await spots.SwitchAsync(id, request.IsOn);
        if (result.IsFailure)
        {
            return await SpotProblems.ForSpotAsync(result.Error, id, db, devices);
        }

        return await ToResultAsync(result, reader, currentActor);
    }

    private static async Task<IResult> ToResultAsync(Result<Spot> result, SpotReader reader, ICurrentActor currentActor)
    {
        if (result.IsFailure)
        {
            return SpotProblems.ToProblem(result.Error);
        }

        var spot = result.Value;
        return await ViewResultAsync(spot.Id, reader, currentActor);
    }
}
