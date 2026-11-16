using Microsoft.EntityFrameworkCore;
using TwoNineteenVolts.Venue.Application.Data;
using TwoNineteenVolts.Venue.Domain.Audit;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Groups;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

public class VenueDbContext(DbContextOptions<VenueDbContext> options) : DbContext(options), IVenueDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Spot> Spots => Set<Spot>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SessionCharge> SessionCharges => Set<SessionCharge>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VenueDbContext).Assembly);
    }
}
