namespace TwoNineteenVolts.Venue.Endpoints.Audit;

public record AuditPageResponse(IReadOnlyList<AuditEntryResponse> Items, int Page, int PageSize, int TotalCount);
