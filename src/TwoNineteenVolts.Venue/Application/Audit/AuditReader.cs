using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit.Contracts;
using TwoNineteenVolts.Venue.Application.Data;

namespace TwoNineteenVolts.Venue.Application.Audit;

public class AuditReader(IVenueDbContext db)
{
    private const int MaxPageSize = 200;

    public async Task<AuditPage> ReadAsync(AuditQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var entries = db.AuditEntries.AsQueryable();
        if (query.From is DateTime from)
        {
            entries = entries.Where(e => e.OccurredAt >= from);
        }

        if (query.To is DateTime to)
        {
            entries = entries.Where(e => e.OccurredAt <= to);
        }

        if (query.UserId is int userId)
        {
            entries = entries.Where(e => e.ActorUserId == userId);
        }

        var action = query.Action;
        if (action is not null)
        {
            entries = entries.Where(e => e.Action == action);
        }

        var totalCount = await entries.CountAsync();
        var items = await entries
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new AuditPage(items, page, pageSize, totalCount);
    }
}
