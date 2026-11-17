namespace TwoNineteenVolts.Venue.Application.Spots.Contracts;

public record SpotSessionView(int Id, DateTime StartedAt, DateTime EndsAt, string StartedBy, long TotalChargedTetri);
