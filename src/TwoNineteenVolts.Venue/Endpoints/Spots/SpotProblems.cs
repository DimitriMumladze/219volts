using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotProblems
{
    public static async Task<IResult> ForSpotAsync(string error, int spotId, IVenueDbContext db, DeviceStatuses statuses)
    {
        if (error != SpotError.StateMismatch)
        {
            return ToProblem(error);
        }

        var spot = await db.Spots.SingleOrDefaultAsync(s => s.Id == spotId);
        if (spot is null || spot.DeviceId is not int deviceId || spot.Channel is not int channel)
        {
            return ToProblem(error);
        }

        var status = statuses.Get(deviceId);
        var realState = status.IsOn(channel);
        var extra = new Dictionary<string, object?> { ["isOn"] = realState };
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
            DeviceError.ErrorAnswer or SpotError.StateMismatch => StatusCodes.Status502BadGateway,
            DeviceError.NoAnswer => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problems.Create(error, statusCode);
    }
}
