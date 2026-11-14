using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Infrastructure.Spots;

public class SpotConfiguration : IEntityTypeConfiguration<Spot>
{
    // SQLite's built-in case-insensitive collation, so "Billiard 1" and "BILLIARD 1" are the same name.
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<Spot> builder)
    {
        builder.HasIndex(s => s.Code)
            .IsUnique();

        builder.Property(s => s.Name)
            .UseCollation(CaseInsensitiveCollation);

        builder.HasIndex(s => s.Name)
            .IsUnique();

        // SQLite treats empty values as distinct, so any number of unmapped spots is allowed.
        builder.HasIndex(s => new { s.DeviceId, s.Channel })
            .IsUnique();

        // No cascade: deleting a device unmaps its spots in DeviceService first.
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
