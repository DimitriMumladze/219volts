namespace TwoNineteenVolts.Venue.Application.Spots;

public record SpotView(
    int Id,
    string Code,
    string Name,
    int GroupId,
    string GroupName,
    int? DeviceId,
    string? DeviceName,
    int? Channel,
    bool? IsOn,
    bool? DeviceOnline,
    DateTime? DeviceLastSeen,
    bool IsArchived,
    DateTime CreatedAt,
    SpotSessionView? Session,
    bool SwitchOffFailed);
