using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Application.Data;

public interface IVenueDbContext
{
    DbSet<User> Users { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    DbSet<Device> Devices { get; }

    DbSet<Spot> Spots { get; }

    DbSet<Group> Groups { get; }

    DbSet<Session> Sessions { get; }

    DbSet<SessionCharge> SessionCharges { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
