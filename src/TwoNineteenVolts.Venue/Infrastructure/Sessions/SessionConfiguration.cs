using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Sessions;
using TwoNineteenVolts.Venue.Domain.Spots;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    private const string OnlyRunning = "\"EndedAt\" IS NULL";

    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasIndex(s => s.SpotId)
            .IsUnique()
            .HasFilter(OnlyRunning);

        builder.HasIndex(s => s.EndsAt);

        builder.Property(s => s.EndReason)
            .HasConversion<string>();

        builder.HasOne<Spot>()
            .WithMany()
            .HasForeignKey(s => s.SpotId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
