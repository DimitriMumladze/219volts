namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record CreateSpotRequest(string Name, int? DeviceId = null, int? Channel = null);
