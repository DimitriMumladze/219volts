using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Devices;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Infrastructure.Spots;

public class SpotConfiguration : IEntityTypeConfiguration<Spot>
{
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<Spot> builder)
    {
        builder.HasIndex(s => s.Code)
            .IsUnique();

        builder.Property(s => s.Name)
            .UseCollation(CaseInsensitiveCollation);

        builder.HasIndex(s => s.NameKey)
            .IsUnique();

        builder.HasIndex(s => new { s.DeviceId, s.Channel })
            .IsUnique();

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
