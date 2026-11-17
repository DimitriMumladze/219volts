using Microsoft.Extensions.Logging;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices;
using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SwitchFailures(IVenueDbContext db, IAuditLog audit, ILogger<SwitchFailures> logger)
{
    private const string NoAnswerReason = "no answer";
    private const string ErrorAnswerReason = "error answer";
    private const string MismatchReason = "mismatch";

    public async Task<string> RecordAsync(int spotId, AuditActor actor, string error)
    {
        var reported = ReportedAs(error);
        if (reported != error)
        {
            return reported;
        }

        Log(spotId, error);

        var reason = ReasonFor(error);
        var target = AuditTarget.Spot(spotId);
        audit.Record(actor, AuditActions.SpotSwitchFailed, target, reason);
        await db.SaveChangesAsync();
        return error;
    }

    public static string ReportedAs(string error)
    {
        if (error == DeviceError.NotFound)
        {
            return SpotError.Unmapped;
        }

        return error;
    }

    public void Log(int spotId, string error)
    {
        var reason = ReasonFor(error);
        logger.LogWarning("Switching spot {SpotId} failed: {Reason}", spotId, reason);
    }

    public static string ReasonFor(string error)
    {
        if (error == DeviceError.NoAnswer)
        {
            return NoAnswerReason;
        }

        if (error == DeviceError.DidNotObey)
        {
            return MismatchReason;
        }

        return ErrorAnswerReason;
    }
}
