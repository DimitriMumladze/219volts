using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwoNineteenVolts.Venue.Domain.Groups;

namespace TwoNineteenVolts.Venue.Infrastructure.Groups;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    private const string CaseInsensitiveCollation = "NOCASE";

    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.Property(g => g.Name)
            .UseCollation(CaseInsensitiveCollation);

        builder.HasIndex(g => g.NameKey)
            .IsUnique();
    }
}
