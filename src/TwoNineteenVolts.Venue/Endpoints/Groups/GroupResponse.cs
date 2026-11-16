namespace TwoNineteenVolts.Venue.Endpoints.Groups;

public record GroupResponse(int Id, string Name, decimal HourlyRate, bool IsArchived);
