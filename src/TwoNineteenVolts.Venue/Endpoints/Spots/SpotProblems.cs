using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Sessions.Contracts;
using TwoNineteenVolts.Venue.Application.Spots;
using TwoNineteenVolts.Venue.Application.Spots.Contracts;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotProblems
{
    public static async Task<IResult> ForSpotAsync(string error, int spotId, IVenueDbContext db, IDeviceGateway devices)
    {
        if (error != DeviceError.DidNotObey)
        {
            return ToProblem(error);
        }

        var spot = await db.Spots.SingleOrDefaultAsync(s => s.Id == spotId);
        if (spot is null || spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return ToProblem(error);
        }

        var outlet = new Outlet(deviceId, channel);
        var state = await devices.StateOfAsync(outlet);
        var extra = new Dictionary<string, object?> { ["isOn"] = state.IsOn };
        return Problems.Create(error, StatusCodes.Status502BadGateway, extra);
    }

    public static IResult ToProblem(string error)
    {
        if (error == SpotError.NameTooLong)
        {
            return Problems.Create(error, StatusCodes.Status400BadRequest, textArguments: [SpotService.MaxNameLength]);
        }

        var statusCode = error switch
        {
            SpotError.NotFound => StatusCodes.Status404NotFound,
            SpotError.InvalidName or SpotError.NameTooLong or SpotError.InvalidMapping
                or SpotError.UnknownDevice or SpotError.ChannelOutOfRange or SpotError.UnknownGroup
                or SessionError.InvalidMinutes or SessionError.InvalidAmount => StatusCodes.Status400BadRequest,
            SpotError.OnlyNameChangeAllowed => StatusCodes.Status403Forbidden,
            SpotError.DuplicateName or SpotError.ChannelInUse or SpotError.Unmapped
                or SpotError.Archived or SpotError.CodeTaken or SpotError.GroupArchived or SpotError.SessionRunning
                or SessionError.AlreadyRunning or SessionError.NotRunning => StatusCodes.Status409Conflict,
            DeviceError.ErrorAnswer or DeviceError.DidNotObey => StatusCodes.Status502BadGateway,
            DeviceError.NoAnswer => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problems.Create(error, statusCode);
    }
}
