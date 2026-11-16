using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Application.Sessions;
using TwoNineteenVolts.Venue.Application.Spots;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public static class SpotProblems
{
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
