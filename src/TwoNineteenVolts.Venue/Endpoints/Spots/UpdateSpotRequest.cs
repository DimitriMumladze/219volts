namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record UpdateSpotRequest(string Name, int? DeviceId, int? Channel);
