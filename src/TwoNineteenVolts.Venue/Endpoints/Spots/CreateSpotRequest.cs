namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record CreateSpotRequest(string Name, int GroupId, int? DeviceId = null, int? Channel = null);
