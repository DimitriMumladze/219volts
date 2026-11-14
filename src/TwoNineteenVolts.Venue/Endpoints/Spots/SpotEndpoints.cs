using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Devices;
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
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}", UpdateAsync).RequireAuthorization(Policies.Manager);
        group.MapPost("/{id:int}/new-code", NewCodeAsync).RequireAuthorization(Policies.Support);
        group.MapPost("/{id:int}/archive", ArchiveAsync).RequireAuthorization(Policies.Support);
        group.MapPut("/{id:int}/state", SwitchAsync);
    }


    private static async Task<IResult> GetSpotsAsync(IVenueDbContext db, DeviceStatuses statuses, ICurrentActor currentActor)
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
        var devices = await db.Devices.ToDictionaryAsync(d => d.Id);

        var responses = visible.Select(s => ToResponse(s, devices, statuses)).ToList();
        return TypedResults.Ok(responses);
    }

    private static async Task<IResult> CreateAsync(CreateSpotRequest request, SpotService spots, IVenueDbContext db, DeviceStatuses statuses)
    {
        var result = await spots.CreateAsync(request.Name, request.DeviceId, request.Channel);
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var spot = result.Value;
        var response = await ToResponseAsync(spot, db, statuses);
        return TypedResults.Created($"/api/spots/{spot.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(int id, UpdateSpotRequest request, SpotService spots, IVenueDbContext db, DeviceStatuses statuses)
    {
        var result = await spots.UpdateAsync(id, request.Name, request.DeviceId, request.Channel);
        return await ToResultAsync(result, db, statuses);
    }

    private static async Task<IResult> NewCodeAsync(int id, SpotService spots, IVenueDbContext db, DeviceStatuses statuses)
    {
        var result = await spots.NewCodeAsync(id);
        return await ToResultAsync(result, db, statuses);
    }

    private static async Task<IResult> ArchiveAsync(int id, SpotService spots, IVenueDbContext db, DeviceStatuses statuses)
    {
        var result = await spots.ArchiveAsync(id);
        return await ToResultAsync(result, db, statuses);
    }

    private static async Task<IResult> SwitchAsync(int id, SwitchSpotRequest request, SpotService spots, IVenueDbContext db, DeviceStatuses statuses)
    {
        var result = await spots.SwitchAsync(id, request.On);
        if (result.Error == SpotError.StateMismatch)
        {
            return await MismatchProblemAsync(id, db, statuses);
        }

        return await ToResultAsync(result, db, statuses);
    }

    private static async Task<IResult> MismatchProblemAsync(int id, IVenueDbContext db, DeviceStatuses statuses)
    {
        var spot = await db.Spots.SingleAsync(s => s.Id == id);
        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return ToProblem(SpotError.StateMismatch);
        }

        var status = statuses.Get(deviceId);
        var realState = status.IsOn(channel);
        var extra = new Dictionary<string, object?> { ["isOn"] = realState };
        return Problems.Create(SpotError.StateMismatch, StatusCodes.Status502BadGateway, extra);
    }

    private static async Task<IResult> ToResultAsync(Result<Spot> result, IVenueDbContext db, DeviceStatuses statuses)
    {
        if (result.IsFailure)
        {
            return ToProblem(result.Error);
        }

        var response = await ToResponseAsync(result.Value, db, statuses);
        return TypedResults.Ok(response);
    }

    private static IResult ToProblem(string error)
    {
        var statusCode = error switch
        {
            SpotError.NotFound => StatusCodes.Status404NotFound,
            SpotError.InvalidName or SpotError.NameTooLong or SpotError.InvalidMapping
                or SpotError.UnknownDevice or SpotError.ChannelOutOfRange => StatusCodes.Status400BadRequest,
            SpotError.OnlyNameChangeAllowed => StatusCodes.Status403Forbidden,
            SpotError.DuplicateName or SpotError.ChannelInUse or SpotError.Unmapped
                or SpotError.Archived or SpotError.CodeTaken => StatusCodes.Status409Conflict,
            DeviceError.ErrorAnswer or SpotError.StateMismatch => StatusCodes.Status502BadGateway,
            DeviceError.NoAnswer => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problems.Create(error, statusCode);
    }

    private static async Task<SpotResponse> ToResponseAsync(Spot spot, IVenueDbContext db, DeviceStatuses statuses)
    {
        var devices = await db.Devices
            .Where(d => d.Id == spot.DeviceId)
            .ToDictionaryAsync(d => d.Id);

        return ToResponse(spot, devices, statuses);
    }

    private static SpotResponse ToResponse(Spot spot, Dictionary<int, Device> devices, DeviceStatuses statuses)
    {
        if (spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return new SpotResponse(spot.Id, spot.Code, spot.Name, null, null, null, null, null, null, spot.IsArchived, spot.CreatedAt);
        }

        var device = devices[deviceId];
        var status = statuses.Get(deviceId);
        var isOn = status.IsOn(channel);

        return new SpotResponse(
            spot.Id,
            spot.Code,
            spot.Name,
            deviceId,
            device.Name,
            channel,
            isOn,
            status.IsOnline,
            status.LastSeen,
            spot.IsArchived,
            spot.CreatedAt);
    }
}
