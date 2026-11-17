using TwoNineteenVolts.Venue.Application.Spots.Contracts;

namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record SpotResponse(
    int Id,
    string Code,
    string Name,
    int GroupId,
    string GroupName,
    int? DeviceId,
    string? DeviceName,
    int? Channel,
    bool? IsOn,
    bool? IsDeviceOnline,
    DateTime? DeviceLastSeen,
    bool IsArchived,
    DateTime CreatedAt,
    SpotSessionResponse? Session,
    bool HasSwitchOffFailed)
{
    public static SpotResponse From(SpotView view)
    {
        SpotSessionResponse? session = null;
        if (view.Session is not null)
        {
            session = SpotSessionResponse.From(view.Session);
        }

        return new SpotResponse(
            view.Id,
            view.Code,
            view.Name,
            view.GroupId,
            view.GroupName,
            view.DeviceId,
            view.DeviceName,
            view.Channel,
            view.IsOn,
            view.IsDeviceOnline,
            view.DeviceLastSeen,
            view.IsArchived,
            view.CreatedAt,
            session,
            view.HasSwitchOffFailed);
    }
}
