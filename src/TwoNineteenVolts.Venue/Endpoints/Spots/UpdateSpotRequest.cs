namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record UpdateSpotRequest(string Name, int GroupId, int? DeviceId, int? Channel);
