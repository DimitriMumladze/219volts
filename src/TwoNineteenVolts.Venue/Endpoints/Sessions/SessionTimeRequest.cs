namespace TwoNineteenVolts.Venue.Endpoints.Sessions;

public record SessionTimeRequest(int Minutes, decimal? Amount = null);
