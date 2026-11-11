using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Data;

// What Application code may do with the database. VenueDbContext (Infrastructure) implements it,
// so services keep LINQ and one SaveChangesAsync without depending on Infrastructure.
public interface IVenueDbContext
{
    DbSet<User> Users { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    // For the rare change that needs more than one SaveChangesAsync to stay all-or-nothing.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
