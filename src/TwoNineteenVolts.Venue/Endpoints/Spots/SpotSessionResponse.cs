namespace TwoNineteenVolts.Venue.Endpoints.Spots;

public record SpotSessionResponse(int Id, DateTime StartedAt, DateTime EndsAt, string StartedBy, decimal TotalCharged);
