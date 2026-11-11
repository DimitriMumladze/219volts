using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Auth;

namespace TwoNineteenVolts.Venue.Infrastructure.Auth;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    // SQLite's built-in case-insensitive collation; the unique index below uses it too,
    // so "Nino" and "NINO" count as the same name.
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
