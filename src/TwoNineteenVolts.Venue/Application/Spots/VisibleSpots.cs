using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Audit;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Spots;

public class VisibleSpots(IVenueDbContext db)
{
    public async Task<Spot?> FindAsync(int id, AuditActor actor)
    {
        var spot = await db.Spots.SingleOrDefaultAsync(s => s.Id == id);
        if (spot is null)
        {
            return null;
        }

        var hidden = !spot.IsMapped || spot.IsArchived;
        if (hidden && actor.Role != Role.Support)
        {
            return null;
        }

        return spot;
    }
}
