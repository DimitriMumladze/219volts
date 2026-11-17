using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Application.Devices.Contracts;
using TwoNineteenVolts.Venue.Application.Spots.Contracts;
using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class SwitchFailures(IVenueDbContext db, IAuditLog audit, ILogger<SwitchFailures> logger)
{
    private const string NoAnswerReason = "no answer";
    private const string ErrorAnswerReason = "error answer";
    private const string MismatchReason = "mismatch";

    public async Task RecordAsync(int spotId, AuditActor actor, string deviceError)
    {
        var isDeviceGone = deviceError == DeviceError.NotFound;
        if (isDeviceGone)
        {
            return;
        }

        Log(spotId, deviceError);

        var reason = AuditReasonFor(deviceError);
        var target = AuditTarget.Spot(spotId);
        audit.Record(actor, AuditActions.SpotSwitchFailed, target, reason);
        await db.SaveChangesAsync();
    }

    public static string ToSpotError(string deviceError)
    {
        var isDeviceGone = deviceError == DeviceError.NotFound;
        if (isDeviceGone)
        {
            return SpotError.Unmapped;
        }

        return deviceError;
    }

    public void Log(int spotId, string deviceError)
    {
        var reason = AuditReasonFor(deviceError);
        logger.LogWarning("Switching spot {SpotId} failed: {Reason}", spotId, reason);
    }

    public static string AuditReasonFor(string deviceError)
    {
        if (deviceError == DeviceError.NoAnswer)
        {
            return NoAnswerReason;
        }

        if (deviceError == DeviceError.DidNotObey)
        {
            return MismatchReason;
        }

        return ErrorAnswerReason;
    }
}
