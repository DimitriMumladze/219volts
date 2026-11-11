using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Application.Data;

// What Application code may do with the database. VenueDbContext (Infrastructure) implements it,
// so services keep LINQ and one SaveChangesAsync without depending on Infrastructure.
public interface IVenueDbContext
{
    DbSet<User> Users { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
