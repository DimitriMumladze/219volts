using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Sessions;

namespace TwoNineteenVolts.Venue.Infrastructure.Sessions;

public class SessionChargeConfiguration : IEntityTypeConfiguration<SessionCharge>
{
    public void Configure(EntityTypeBuilder<SessionCharge> builder)
    {
        builder.HasIndex(c => c.At);

        builder.Property(c => c.Kind)
            .HasConversion<string>();

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(c => c.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
