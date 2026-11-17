using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Infrastructure.Auth;

namespace TwoNineteenVolts.Venue.Endpoints.Audit;

public static class AuditEndpoints
{
    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").RequireAuthorization(Policies.Manager);

        group.MapGet("/", GetEntriesAsync);
    }


    private static async Task<IResult> GetEntriesAsync(
        AuditReader reader,
        DateTime? from,
        DateTime? to,
        int? userId,
        string? action,
        int page = 1,
        int pageSize = 50)
    {
        var result = await reader.ReadAsync(new AuditQuery(from, to, userId, action, page, pageSize));

        var items = result.Items.Select(ToResponse).ToList();
        var response = new AuditPageResponse(items, result.Page, result.PageSize, result.TotalCount);

        return TypedResults.Ok(response);
    }

    private static AuditEntryResponse ToResponse(AuditEntry entry) =>
        new(entry.Id, entry.OccurredAt, entry.ActorUserId, entry.ActorName, entry.Action, entry.Target, entry.Details);
}
