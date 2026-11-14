using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Devices;

namespace TwoNineteenVolts.Venue.Infrastructure.Devices;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    // SQLite's built-in case-insensitive collation, so "Panel 1" and "PANEL 1" are the same name.
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.Property(d => d.Name)
            .UseCollation(CaseInsensitiveCollation);

        builder.HasIndex(d => d.Name)
            .IsUnique();

        builder.HasIndex(d => new { d.IpAddress, d.Port })
            .IsUnique();

        builder.Property(d => d.Kind)
            .HasConversion<string>();
    }
}
