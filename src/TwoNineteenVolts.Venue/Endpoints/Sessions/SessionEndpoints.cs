using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Common;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Endpoints.Spots;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Sessions;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/spots/{id:int}").RequireAuthorization(Policies.Staff);

        group.MapPost("/session", StartAsync);
        group.MapPost("/session/extend", ExtendAsync);
        group.MapPost("/session/stop", StopAsync);
        group.MapGet("/price", PriceAsync);
    }


    private static async Task<IResult> StartAsync(
        int id,
        SessionTimeRequest request,
        SessionService sessions,
        IVenueDbContext db,
        IDeviceGateway devices,
        SwitchOffWarnings warnings)
    {
        var result = await sessions.StartAsync(id, request.Minutes, request.Amount);
        return await ToResultAsync(id, result, db, devices, warnings);
    }

    private static async Task<IResult> ExtendAsync(
        int id,
        SessionTimeRequest request,
        SessionService sessions,
        IVenueDbContext db,
        IDeviceGateway devices,
        SwitchOffWarnings warnings)
    {
        var result = await sessions.ExtendAsync(id, request.Minutes, request.Amount);
        return await ToResultAsync(id, result, db, devices, warnings);
    }

    private static async Task<IResult> StopAsync(int id, SessionService sessions, IVenueDbContext db, IDeviceGateway devices, SwitchOffWarnings warnings)
    {
        var result = await sessions.StopAsync(id);
        return await ToResultAsync(id, result, db, devices, warnings);
    }

    private static async Task<IResult> PriceAsync(int id, int minutes, SessionService sessions)
    {
        var result = await sessions.PriceAsync(id, minutes);
        if (result.IsFailure)
        {
            return SpotProblems.ToProblem(result.Error);
        }

        var amount = Money.ToGel(result.Value);
        var response = new PriceResponse(amount);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ToResultAsync(
        int id,
        Result<Session> result,
        IVenueDbContext db,
        IDeviceGateway devices,
        SwitchOffWarnings warnings)
    {
        if (result.IsFailure)
        {
            return await SpotProblems.ForSpotAsync(result.Error, id, db, devices);
        }

        var spot = await db.Spots.FindAsync(id);
        if (spot is null)
        {
            return SpotProblems.ToProblem(SpotError.NotFound);
        }

        var response = await SpotResponses.BuildAsync(spot, db, devices, warnings);
        return TypedResults.Ok(response);
    }
}
