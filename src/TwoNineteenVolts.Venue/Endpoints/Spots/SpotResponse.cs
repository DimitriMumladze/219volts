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
    bool? DeviceOnline,
    DateTime? DeviceLastSeen,
    bool IsArchived,
    DateTime CreatedAt,
    SpotSessionResponse? Session,
    bool SwitchOffFailed);
