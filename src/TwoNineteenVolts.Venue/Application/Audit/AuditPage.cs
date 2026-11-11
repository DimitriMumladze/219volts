using TwoNineteenVolts.Venue.Domain.Audit;

namespace TwoNineteenVolts.Venue.Application.Audit;

public record AuditPage(IReadOnlyList<AuditEntry> Items, int Page, int PageSize, int TotalCount);
