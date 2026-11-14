using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Users;

namespace TwoNineteenVolts.Venue.Infrastructure.Users;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName)
            .UseCollation(CaseInsensitiveCollation);

        builder.HasIndex(u => u.DisplayName)
            .IsUnique();

        builder.Property(u => u.Role)
            .HasConversion<string>();
    }
}
