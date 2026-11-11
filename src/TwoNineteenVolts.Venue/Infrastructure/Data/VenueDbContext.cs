using Microsoft.EntityFrameworkCore;

namespace TwoNineteenVolts.Venue.Infrastructure.Data;

public class VenueDbContext(DbContextOptions<VenueDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VenueDbContext).Assembly);
    }
}
