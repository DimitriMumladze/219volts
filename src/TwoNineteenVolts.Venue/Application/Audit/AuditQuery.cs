namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditQuery(DateTime? From, DateTime? To, int? UserId, string? Action, int Page = 1, int PageSize = 50);
